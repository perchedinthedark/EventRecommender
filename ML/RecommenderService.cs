using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.EntityFrameworkCore;
using EventRecommender.Data;
using EventRecommender.Models;
using Microsoft.ML.Trainers;

namespace EventRecommender.ML
{
    public interface IRecommenderService
    {
        Task TrainAsync();
        Task<int[]> RecommendForUserAsync(string userId, int topN = 10);
        bool ModelsExist();
    }

    public sealed class RecommenderService : IRecommenderService
    {
        private readonly AppDbContext _db;
        private readonly RecommenderConfig _cfg;
        private readonly MLContext _ml;

        public RecommenderService(AppDbContext db, RecommenderConfig cfg)
        {
            _db = db;
            _cfg = cfg;
            _ml = new MLContext(seed: 42);
            Directory.CreateDirectory(_cfg.ModelDir);
        }

        public bool ModelsExist()
            => File.Exists(_cfg.MfModelPath) && File.Exists(_cfg.RankModelPath);

        // ---------- TRAIN ----------
        public async Task TrainAsync()
        {
            // 1) Extract interactions & clicks
            // Collapse to a single preference score per (user,event)
            var interactions = await _db.UserEventInteractions
                .Select(i => new { i.UserId, i.EventId, i.Status, i.Rating })
                .ToListAsync();

            var clicks = await _db.EventClicks
                .Select(c => new { c.UserId, c.EventId })
                .ToListAsync();

            // Build preference dictionary (userId,eventId) -> label
            var prefs = new System.Collections.Generic.Dictionary<(string, int), float>();

            void Upsert(string? uid, int eid, float val)
            {
                if (string.IsNullOrEmpty(uid)) return;
                var key = (uid!, eid);
                if (!prefs.TryGetValue(key, out var cur) || val > cur)
                    prefs[key] = val;
            }

            // clicks = weak signal
            foreach (var c in clicks)
                Upsert(c.UserId, c.EventId, _cfg.ScoreView);

            // explicit signals
            foreach (var i in interactions)
            {
                var baseScore = i.Status switch
                {
                    InteractionStatus.Interested => _cfg.ScoreInterested,
                    InteractionStatus.Going => _cfg.ScoreGoing,
                    _ => 0f
                };
                var rated = i.Rating.HasValue ? Math.Max(baseScore, _cfg.ScoreRated(i.Rating.Value)) : baseScore;
                if (rated > 0f) Upsert(i.UserId, i.EventId, rated);
            }

            // Map string ids to keys (uint) for MF
            var allUsers = prefs.Keys.Select(k => k.Item1).Distinct().ToArray();
            var allEvents = await _db.Events.Select(e => e.EventId).Distinct().ToArrayAsync();

            var userToIdx = allUsers.Select((u, i) => (u, (uint)(i + 1))).ToDictionary(x => x.u, x => x.Item2);
            var eventToIdx = allEvents.Select((e, i) => (e, (uint)(i + 1))).ToDictionary(x => x.e, x => x.Item2);

            var mfRows = prefs
                .Where(kv => userToIdx.ContainsKey(kv.Key.Item1) && eventToIdx.ContainsKey(kv.Key.Item2))
                .Select(kv => new MfRow
                {
                    UserId = userToIdx[kv.Key.Item1],
                    EventId = eventToIdx[kv.Key.Item2],
                    Label = kv.Value
                }).ToList();

            if (mfRows.Count < 20)
                throw new InvalidOperationException("Not enough interaction data to train. Create some users/events and interact with them first.");

            var mfData = _ml.Data.LoadFromEnumerable(mfRows);

            // 2) Stage A: Matrix Factorization (candidate generation)
            var mfOptions = new MatrixFactorizationTrainer.Options
            {
                MatrixColumnIndexColumnName = nameof(MfRow.UserId),   // key/index column
                MatrixRowIndexColumnName = nameof(MfRow.EventId),  // key/index column
                LabelColumnName = nameof(MfRow.Label),
                NumberOfIterations = 50,
                ApproximationRank = 64,
                Alpha = 0.01f,
                Lambda = 0.025f
            };

            var mfPipeline = _ml.Recommendation().Trainers.MatrixFactorization(mfOptions);


            var mfModel = mfPipeline.Fit(mfData);
            _ml.Model.Save(mfModel, mfData.Schema, _cfg.MfModelPath);

            // 3) Build ranking training rows using positive/negative sampling
            // For each user, positives = interacted events; negatives = some random non-interacted events
            var rnd = new Random(123);
            var byUser = prefs
                .GroupBy(kv => kv.Key.Item1)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Key.Item2).ToHashSet());

            var allEventIds = allEvents.ToHashSet();

            var rankRows = new System.Collections.Generic.List<RankRow>();

            // simple organizer popularity proxy: count of events (could be made better later)
            var organizerPop = await _db.Events
                .GroupBy(e => e.OrganizerId)
                .Select(g => new { g.Key, Cnt = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => (float)x.Cnt);

            foreach (var u in byUser.Keys)
            {
                var positives = byUser[u].ToList();
                if (positives.Count == 0) continue;

                // sample some negatives
                var negPool = allEventIds.Except(positives).ToArray();
                var negSample = negPool.OrderBy(_ => rnd.Next()).Take(Math.Min(positives.Count * 3, 100)).ToArray();

                // build features
                void AddRows(int evtId, float label)
                {
                    var e = _db.Events.AsNoTracking()
                        .Select(x => new { x.EventId, x.CategoryId, x.VenueId, x.OrganizerId, x.DateTime })
                        .First(x => x.EventId == evtId);

                    var venue = _db.Venues.AsNoTracking().FirstOrDefault(v => v.VenueId == e.VenueId);
                    var capacity = venue?.Capacity ?? 0;

                    var daysAgo = (float)Math.Max(0, (DateTime.UtcNow - e.DateTime.ToUniversalTime()).TotalDays);
                    var recency = MathF.Exp(-daysAgo / 30f); // 30d half-life-ish

                    var hour = (float)e.DateTime.Hour;
                    var dow = (float)((int)e.DateTime.DayOfWeek);

                    organizerPop.TryGetValue(e.OrganizerId, out var orgScore);

                    rankRows.Add(new RankRow
                    {
                        Label = label,
                        GroupId = HashToFloat(u), // group by user
                        EventRecency = recency,
                        OrganizerScore = orgScore,
                        VenueCapacity = capacity,
                        CategoryId = e.CategoryId,
                        HourOfDay = hour,
                        DayOfWeek = dow
                    });
                }

                foreach (var p in positives) AddRows(p, 1f);
                foreach (var n in negSample) AddRows(n, 0f);
            }

            var rankData = _ml.Data.LoadFromEnumerable(rankRows);

            // 4) Stage B: LightGBM Ranking
            var features = new[] { nameof(RankRow.EventRecency), nameof(RankRow.OrganizerScore), nameof(RankRow.VenueCapacity),
                                   nameof(RankRow.CategoryId), nameof(RankRow.HourOfDay), nameof(RankRow.DayOfWeek) };

            var rankPipeline =
                _ml.Transforms.Concatenate("Features", features)
                 .Append(_ml.Ranking.Trainers.FastTree(new Microsoft.ML.Trainers.FastTree.FastTreeRankingTrainer.Options
                 {
                     LabelColumnName = nameof(RankRow.Label),
                     FeatureColumnName = "Features",
                     RowGroupColumnName = nameof(RankRow.GroupId),
                     NumberOfTrees = 100,         // ensemble size
                     NumberOfLeaves = 32,         // depth-ish
                     MinimumExampleCountPerLeaf = 10,
                     LearningRate = 0.2
                 }));


            var rankModel = rankPipeline.Fit(rankData);
            _ml.Model.Save(rankModel, rankData.Schema, _cfg.RankModelPath);
        }

        // ---------- RECOMMEND ----------
        public async Task<int[]> RecommendForUserAsync(string userId, int topN = 10)
        {
            if (!ModelsExist()) return Array.Empty<int>();

            // Load models
            DataViewSchema mfSchema, rankSchema;
            var mfModel = _ml.Model.Load(_cfg.MfModelPath, out mfSchema);
            var rankModel = _ml.Model.Load(_cfg.RankModelPath, out rankSchema);

            var mfEngine = _ml.Model.CreatePredictionEngine<MfRow, MfScore>(mfModel, ignoreMissingColumns: true);
            var rankEngine = _ml.Model.CreatePredictionEngine<RankRow, RankPrediction>(rankModel, ignoreMissingColumns: true);


            var allEvents = await _db.Events
                .AsNoTracking()
                .Select(e => e.EventId)
                .ToListAsync();

            // If user has interactions, encode them; otherwise cold-start fallback: just rank all by features.
            var userInteractions = await _db.UserEventInteractions
                .Where(i => i.UserId == userId)
                .Select(i => i.EventId)
                .ToListAsync();

            var candidateIds = allEvents;

            // Stage A: score all events with MF and take top K
            // We need a mapping from string user → uint key. For serving, a quick trick is to hash to a stable uint key-space.
            // (In production you’d persist the dictionary from training.)
            uint ukey = StableKey(userId);

            var mfTop = candidateIds
                .Select(eid => new
                {
                    EventId = eid,
                    Score = mfEngine.Predict(new MfRow { UserId = ukey, EventId = StableKey(eid), Label = 0f }).Score
                })
                .OrderByDescending(x => x.Score)
                .Take(_cfg.CandidatesPerUser)
                .Select(x => x.EventId)
                .ToList();

            if (mfTop.Count == 0) mfTop = candidateIds; // cold start fallback

            // Stage B: rank candidates with features
            var ranked = mfTop
                .Select(eid =>
                {
                    var e = _db.Events.AsNoTracking()
                        .Select(x => new { x.EventId, x.CategoryId, x.VenueId, x.OrganizerId, x.DateTime })
                        .First(x => x.EventId == eid);

                    var venue = _db.Venues.AsNoTracking().FirstOrDefault(v => v.VenueId == e.VenueId);
                    var capacity = venue?.Capacity ?? 0;

                    var daysAgo = (float)Math.Max(0, (DateTime.UtcNow - e.DateTime.ToUniversalTime()).TotalDays);
                    var recency = MathF.Exp(-daysAgo / 30f);

                    var hour = (float)e.DateTime.Hour;
                    var dow = (float)((int)e.DateTime.DayOfWeek);

                    var orgScore = _db.Events.Count(x => x.OrganizerId == e.OrganizerId); // quick proxy

                    var row = new RankRow
                    {
                        Label = 0f,
                        GroupId = HashToFloat(userId),
                        EventRecency = recency,
                        OrganizerScore = orgScore,
                        VenueCapacity = capacity,
                        CategoryId = e.CategoryId,
                        HourOfDay = hour,
                        DayOfWeek = dow
                    };

                    var score = rankEngine.Predict(row).Score;
                    return (eid, score);
                })
                .OrderByDescending(x => x.score)
                .Take(topN)
                .Select(x => x.eid)
                .ToArray();

            return ranked;
        }

        // ----- helpers -----
        private static uint StableKey(string s)
            => (uint)(unchecked((int)Fnv1a32(s)) & int.MaxValue) + 1;

        private static uint StableKey(int id)
            => (uint)id + 1;

        private static uint Fnv1a32(string s)
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            uint hash = offset;
            foreach (var ch in s)
            {
                hash ^= ch;
                hash *= prime;
            }
            return hash;
        }

        private sealed class MfScore
        {
            public float Score { get; set; }
        }

        private static float HashToFloat(string s)
            => BitConverter.Int32BitsToSingle(unchecked((int)Fnv1a32(s)));
    }
}
