using EventRecommender.Data;
using EventRecommender.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

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

            // Use stable hashing for both training and serving
            var allEvents = await _db.Events.Select(e => e.EventId).Distinct().ToArrayAsync();

            // Build MF training rows directly from raw IDs (no hashing needed)
            var mfRows = prefs.Select(kv => new MfRow
            {
                UserId = kv.Key.Item1,   // string userId
                EventId = kv.Key.Item2,  // int eventId
                Label = kv.Value
            }).ToList();

            if (mfRows.Count < 20)
                throw new InvalidOperationException("Not enough interaction data to train. Create some users/events and interact with them first.");

            var mfData = _ml.Data.LoadFromEnumerable(mfRows);

            // Stage A: Map raw IDs -> Key types, then train MF
            var mfPipeline =
                _ml.Transforms.Conversion.MapValueToKey(outputColumnName: "UserIdKey", inputColumnName: nameof(MfRow.UserId))
                  .Append(_ml.Transforms.Conversion.MapValueToKey(outputColumnName: "EventIdKey", inputColumnName: nameof(MfRow.EventId)))
                  .Append(_ml.Recommendation().Trainers.MatrixFactorization(new MatrixFactorizationTrainer.Options
                  {
                      MatrixColumnIndexColumnName = "UserIdKey",  // Key type
                      MatrixRowIndexColumnName = "EventIdKey", // Key type
                      LabelColumnName = nameof(MfRow.Label),
                      NumberOfIterations = 50,
                      ApproximationRank = 64,
                      Alpha = 0.01f,
                      Lambda = 0.025f
                  }));

            var mfModel = mfPipeline.Fit(mfData);
            _ml.Model.Save(mfModel, mfData.Schema, _cfg.MfModelPath);

            // 3) Build ranking training rows using positive/negative sampling
            // For each user, positives = interacted events; negatives = some random non-interacted events
            var rnd = new Random(123);
            var byUser = prefs
                .GroupBy(kv => kv.Key.Item1)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Key.Item2).ToHashSet());

            var allEventIds = allEvents.ToHashSet();

            // ... after MF model is saved ...

            // byUser: Dictionary<string userId, HashSet<int eventId>> already computed
            var rankRows = new System.Collections.Generic.List<RankRow>();

            foreach (var u in byUser.Keys)
            {
                var positives = byUser[u].ToList();
                if (positives.Count == 0) continue;

                var negPool = allEventIds.Except(positives).ToArray();
                var negSample = negPool.OrderBy(_ => rnd.Next()).Take(Math.Min(positives.Count * 3, 100)).ToArray();

                void AddRows(int evtId, float label)
                {
                    var e = _db.Events.AsNoTracking()
                        .Select(x => new { x.EventId, x.CategoryId, x.VenueId, x.OrganizerId, x.DateTime })
                        .First(x => x.EventId == evtId);

                    var venue = _db.Venues.AsNoTracking().FirstOrDefault(v => v.VenueId == e.VenueId);
                    var capacity = (float)(venue?.Capacity ?? 0);

                    var daysAgo = (float)Math.Max(0, (DateTime.UtcNow - e.DateTime.ToUniversalTime()).TotalDays);
                    var recency = MathF.Exp(-daysAgo / 30f);

                    var hour = (float)e.DateTime.Hour;
                    var dow = (float)((int)e.DateTime.DayOfWeek);

                    var orgScore = (float)_db.Events.Count(x => x.OrganizerId == e.OrganizerId);

                    rankRows.Add(new RankRow
                    {
                        Label = label,
                        GroupId = u, // <-- raw userId; will map to Key
                        EventRecency = recency,
                        OrganizerScore = orgScore,
                        VenueCapacity = capacity,
                        CategoryId = (float)e.CategoryId,
                        HourOfDay = hour,
                        DayOfWeek = dow
                    });
                }

                foreach (var p in positives) AddRows(p, 1f);
                foreach (var n in negSample) AddRows(n, 0f);
            }

            var rankData = _ml.Data.LoadFromEnumerable(rankRows);

            var features = new[]
            {
    nameof(RankRow.EventRecency),
    nameof(RankRow.OrganizerScore),
    nameof(RankRow.VenueCapacity),
    nameof(RankRow.CategoryId),
    nameof(RankRow.HourOfDay),
    nameof(RankRow.DayOfWeek)
};

            var rankPipeline =
                _ml.Transforms.Conversion.MapValueToKey(outputColumnName: "GroupIdKey", inputColumnName: nameof(RankRow.GroupId))
                  .Append(_ml.Transforms.Concatenate("Features", features))
                  .Append(_ml.Ranking.Trainers.FastTree(new Microsoft.ML.Trainers.FastTree.FastTreeRankingTrainer.Options
                  {
                      LabelColumnName = nameof(RankRow.Label),
                      FeatureColumnName = "Features",
                      RowGroupColumnName = "GroupIdKey",
                      NumberOfTrees = 100,
                      NumberOfLeaves = 32,
                      MinimumExampleCountPerLeaf = 10,
                      LearningRate = 0.2
                  }));

            var rankModel = rankPipeline.Fit(rankData);
            _ml.Model.Save(rankModel, rankData.Schema, _cfg.RankModelPath);

        }

        // ---------- RECOMMEND ----------
        public async Task<int[]> RecommendForUserAsync(string userId, int topN = 10)
        {
            if (!ModelsExist())
                return await PopularEventsAsync(topN);

            var explicitCount = await _db.UserEventInteractions.CountAsync(i => i.UserId == userId);
            var clickCount = await _db.EventClicks.CountAsync(c => c.UserId == userId);
            if (explicitCount + clickCount == 0)
                return await PopularEventsAsync(topN);

            DataViewSchema mfSchema, rankSchema;
            var mfModel = _ml.Model.Load(_cfg.MfModelPath, out mfSchema);
            var rankModel = _ml.Model.Load(_cfg.RankModelPath, out rankSchema);

            var mfEngine = _ml.Model.CreatePredictionEngine<MfRow, MfScore>(mfModel, ignoreMissingColumns: true);
            var rankEngine = _ml.Model.CreatePredictionEngine<RankRow, RankPrediction>(rankModel, ignoreMissingColumns: true);

            var allEvents = await _db.Events
                .AsNoTracking()
                .Select(e => e.EventId)
                .ToListAsync();

            // Stage A: score all candidates with MF and take top-K
            var mfCandidates = allEvents
                .Select(eid => new { EventId = eid, Mf = (double)mfEngine.Predict(new MfRow { UserId = userId, EventId = eid, Label = 0f }).Score })
                .OrderByDescending(x => x.Mf)
                .Take(_cfg.CandidatesPerUser)
                .ToList();

            if (mfCandidates.Count == 0)
                return await PopularEventsAsync(topN);

            // Stage B: get ranker score per candidate
            var scored = new List<(int eid, double mf, double rk)>();
            foreach (var c in mfCandidates)
            {
                var e = _db.Events.AsNoTracking()
                    .Select(x => new { x.EventId, x.CategoryId, x.VenueId, x.OrganizerId, x.DateTime })
                    .First(x => x.EventId == c.EventId);

                var venue = _db.Venues.AsNoTracking().FirstOrDefault(v => v.VenueId == e.VenueId);
                var capacity = (float)(venue?.Capacity ?? 0);

                var daysAgo = (float)Math.Max(0, (DateTime.UtcNow - e.DateTime.ToUniversalTime()).TotalDays);
                var recency = MathF.Exp(-daysAgo / 30f);

                var hour = (float)e.DateTime.Hour;
                var dow = (float)((int)e.DateTime.DayOfWeek);
                var orgScore = (float)_db.Events.Count(x => x.OrganizerId == e.OrganizerId);

                var row = new RankRow
                {
                    Label = 0f,
                    GroupId = userId,
                    EventRecency = recency,
                    OrganizerScore = orgScore,
                    VenueCapacity = capacity,
                    CategoryId = (float)e.CategoryId,
                    HourOfDay = hour,
                    DayOfWeek = dow
                };

                var rk = (double)rankEngine.Predict(row).Score;
                scored.Add((c.EventId, c.Mf, rk));
            }

            // Normalize and blend (tweak weights if you like)
            static double Norm(double v, double min, double max) => max > min ? (v - min) / (max - min) : 0.5;
            var mfMin = scored.Min(s => s.mf); var mfMax = scored.Max(s => s.mf);
            var rkMin = scored.Min(s => s.rk); var rkMax = scored.Max(s => s.rk);

            const double wMf = 0.65; // MF weight
            const double wRk = 0.35; // Ranker weight

            var final = scored
                .Select(s => new {
                    s.eid,
                    score = wMf * Norm(s.mf, mfMin, mfMax) + wRk * Norm(s.rk, rkMin, rkMax)
                })
                .OrderByDescending(x => x.score)
                .Take(topN)
                .Select(x => x.eid)
                .ToArray();

            // Optional: log shown recs
            var now = DateTime.UtcNow;
            foreach (var eid in final)
                _db.RecommendationLogs.Add(new RecommendationLog { UserId = userId, EventId = eid, RecommendedAt = now });
            await _db.SaveChangesAsync();

            return final;
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

        private async Task<int[]> PopularEventsAsync(int topN)
        {
            var now = DateTime.UtcNow;
            var cutoff = now.AddDays(-30);

            // 1) Aggregate popularity signals on the server
            var clickStats = await _db.EventClicks
                .AsNoTracking()
                .Where(c => c.ClickedAt >= cutoff)
                .GroupBy(c => c.EventId)
                .Select(g => new
                {
                    EventId = g.Key,
                    Clicks = g.Count(),
                    // sum as long? to be safe; null coalesce then to 0
                    DwellTotal = (long?)g.Sum(c => (long?)(c.DwellMs ?? 0))
                })
                .ToListAsync();

            // 2) Build a fast lookup for scores
            var scoreByEvent = clickStats.ToDictionary(
                x => x.EventId,
                x => (double)x.Clicks + ((double)(x.DwellTotal ?? 0) / 2000.0)
            );

            // 3) Pull minimal event info
            var events = await _db.Events
                .AsNoTracking()
                .Select(e => new { e.EventId, e.DateTime })
                .ToListAsync();

            // 4) Join + order IN MEMORY (avoid EF ORDER BY translation issues)
            var ids = events
                .Select(e => new
                {
                    e.EventId,
                    e.DateTime,
                    Score = scoreByEvent.TryGetValue(e.EventId, out var s) ? s : 0.0
                })
                .OrderByDescending(x => x.DateTime >= now) // upcoming first
                .ThenByDescending(x => x.Score)            // then popularity
                .ThenBy(x => x.DateTime)                   // then soonest
                .Take(topN)
                .Select(x => x.EventId)
                .ToArray();

            // Fallback: if no events/signals, just upcoming
            if (ids.Length == 0)
            {
                ids = await _db.Events
                    .AsNoTracking()
                    .OrderBy(e => e.DateTime)
                    .Take(topN)
                    .Select(e => e.EventId)
                    .ToArrayAsync();
            }

            return ids;
        }






    }
}
