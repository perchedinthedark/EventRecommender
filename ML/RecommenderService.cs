using EventRecommender.Data;
using EventRecommender.Models;
using EventRecommender.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Trainers.FastTree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private readonly ITrendingService _trending;
        private readonly MLContext _ml;

        private const int ENGAGEMENT_WINDOW_DAYS = 30;
        private const int NEG_PER_POS_MULT = 3;
        private const int MAX_NEG_PER_USER = 100;

        public RecommenderService(AppDbContext db, RecommenderConfig cfg, ITrendingService trending)
        {
            _db = db;
            _cfg = cfg;
            _trending = trending;
            _ml = new MLContext(seed: 42);
            Directory.CreateDirectory(_cfg.ModelDir);
        }

        public bool ModelsExist()
            => File.Exists(_cfg.MfModelPath) && File.Exists(_cfg.RankModelPath);

        // -------------------- TRAIN --------------------
        public async Task TrainAsync()
        {
            // 0) Pull interaction signals
            var interactionsRaw = await _db.UserEventInteractions
                .AsNoTracking()
                .Select(i => new { i.UserId, i.EventId, i.Status, i.Rating })
                .ToListAsync();

            var clicksRaw = await _db.EventClicks
                .AsNoTracking()
                .Select(c => new { c.UserId, c.EventId })
                .ToListAsync();

            // Collapse to preference per (user,event)
            var prefs = new Dictionary<(string uid, int eid), float>();
            void Upsert(string? uid, int eid, float val)
            {
                if (string.IsNullOrEmpty(uid)) return;
                var k = (uid!, eid);
                if (!prefs.TryGetValue(k, out var cur) || val > cur) prefs[k] = val;
            }

            foreach (var c in clicksRaw)
                Upsert(c.UserId, c.EventId, _cfg.ScoreView);

            foreach (var i in interactionsRaw)
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

            if (prefs.Count < 20)
                throw new InvalidOperationException("Not enough interaction data to train. Create users/events and interact first.");

            // 1) Stage A: MF training (string IDs; pipeline maps to keys)
            var mfRows = prefs.Select(kv => new MfRow
            {
                UserId = kv.Key.uid,
                EventId = kv.Key.eid.ToString(),
                Label = kv.Value
            });

            var mfData = _ml.Data.LoadFromEnumerable(mfRows);

            var mfPipeline =
                _ml.Transforms.Conversion.MapValueToKey("UserKey", nameof(MfRow.UserId), addKeyValueAnnotationsAsText: true)
                  .Append(_ml.Transforms.Conversion.MapValueToKey("ItemKey", nameof(MfRow.EventId), addKeyValueAnnotationsAsText: true))
                  .Append(_ml.Recommendation().Trainers.MatrixFactorization(new Microsoft.ML.Trainers.MatrixFactorizationTrainer.Options
                  {
                      MatrixColumnIndexColumnName = "UserKey",
                      MatrixRowIndexColumnName = "ItemKey",
                      LabelColumnName = nameof(MfRow.Label),
                      NumberOfIterations = 60,
                      ApproximationRank = 64,
                      Alpha = 0.01f,
                      Lambda = 0.025f
                  }));

            var mfModel = mfPipeline.Fit(mfData);
            _ml.Model.Save(mfModel, mfData.Schema, _cfg.MfModelPath);

            // Reuse MF at training-time to compute MFScore feature.
            var mfEngine = _ml.Model.CreatePredictionEngine<MfRow, MfScore>(mfModel, ignoreMissingColumns: true);

            // 2) Precompute feature materials for ranking
            var now = DateTime.UtcNow;
            var cutoff = now.AddDays(-ENGAGEMENT_WINDOW_DAYS);

            var eventsBasic = await _db.Events.AsNoTracking()
                .Select(e => new { e.EventId, e.CategoryId, e.VenueId, e.OrganizerId, e.DateTime })
                .ToListAsync();
            var eventById = eventsBasic.ToDictionary(x => x.EventId);

            var venueById = await _db.Venues.AsNoTracking()
                .ToDictionaryAsync(v => v.VenueId, v => v);

            var organizerPop = await _db.Events.AsNoTracking()
                .GroupBy(e => e.OrganizerId)
                .Select(g => new { g.Key, Cnt = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => (float)x.Cnt);

            var friendsMap = await _db.Friendships.AsNoTracking()
                .GroupBy(f => f.FollowerId)
                .Select(g => new { Follower = g.Key, Friends = g.Select(x => x.FolloweeId).ToList() })
                .ToDictionaryAsync(x => x.Follower, x => x.Friends.ToHashSet());

            // All interactions (for social + affinity & totals)
            var interAll = await _db.UserEventInteractions.AsNoTracking()
                .Select(i => new { i.UserId, i.EventId, i.Status, i.Rating })
                .ToListAsync();

            var interByEvent = interAll
                .GroupBy(i => i.EventId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // User -> (CategoryId -> weighted sum)
            var catWeightByUser = new Dictionary<string, Dictionary<int, float>>();
            foreach (var i in interAll)
            {
                if (!eventById.TryGetValue(i.EventId, out var eBase)) continue;
                var weight = Math.Max(
                    i.Status switch
                    {
                        InteractionStatus.Interested => _cfg.ScoreInterested,
                        InteractionStatus.Going => _cfg.ScoreGoing,
                        _ => 0f
                    },
                    i.Rating.HasValue ? _cfg.ScoreRated(i.Rating.Value) : 0f
                );

                if (weight <= 0) continue;

                if (!catWeightByUser.TryGetValue(i.UserId, out var catMap))
                {
                    catMap = new Dictionary<int, float>();
                    catWeightByUser[i.UserId] = catMap;
                }
                catMap.TryGetValue(eBase.CategoryId, out var cur);
                catMap[eBase.CategoryId] = cur + weight;
            }

            // Event engagement window aggregates
            var clicks30 = await _db.EventClicks.AsNoTracking()
                .Where(c => c.ClickedAt >= cutoff)
                .GroupBy(c => c.EventId)
                .Select(g => new
                {
                    EventId = g.Key,
                    Cnt = g.Count(),
                    Dwell = g.Sum(x => (double)(x.DwellMs ?? 0)) / 2000.0
                })
                .ToListAsync();

            var clicks30Cnt = clicks30.ToDictionary(x => x.EventId, x => (float)x.Cnt);
            var clicks30Dwell = clicks30.ToDictionary(x => x.EventId, x => (float)x.Dwell);

            // 3) Build ranking training rows (positive/negative sampling)
            var byUserPosEvents = prefs
                .GroupBy(p => p.Key.uid)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Key.eid).ToHashSet());

            var allEventIds = eventsBasic.Select(e => e.EventId).ToArray();
            var rnd = new Random(123);

            var rankRows = new List<RankRow>();

            float RecencyScore(DateTime dtUtc)
            {
                var daysAgo = (float)Math.Max(0, (now - dtUtc.ToUniversalTime()).TotalDays);
                return MathF.Exp(-daysAgo / 30f);
            }

            void AddRowsForUser(string uid, int evtId, float label)
            {
                if (!eventById.TryGetValue(evtId, out var e)) return;

                venueById.TryGetValue(e.VenueId, out var venue);
                var capacity = (float)(venue?.Capacity ?? 0);

                organizerPop.TryGetValue(e.OrganizerId, out var orgScore);

                // social wrt user's friends (ratios)
                friendsMap.TryGetValue(uid, out var friends);
                friends ??= new HashSet<string>();

                float frGoingRatio = 0f, frInterestedRatio = 0f, frAvgRating01 = 0f;
                if (interByEvent.TryGetValue(evtId, out var inters))
                {
                    // Totals across all users
                    var totalGoing = inters.Count(x => x.Status == InteractionStatus.Going);
                    var totalInterested = inters.Count(x => x.Status == InteractionStatus.Interested);

                    var fInters = inters.Where(x => friends.Contains(x.UserId)).ToList();
                    var frGoing = fInters.Count(x => x.Status == InteractionStatus.Going);
                    var frInterested = fInters.Count(x => x.Status == InteractionStatus.Interested);

                    frGoingRatio = totalGoing > 0 ? (float)frGoing / totalGoing : 0f;
                    frInterestedRatio = totalInterested > 0 ? (float)frInterested / totalInterested : 0f;

                    var ratings = fInters.Where(x => x.Rating.HasValue).Select(x => x.Rating!.Value).ToList();
                    frAvgRating01 = ratings.Count > 0 ? (float)ratings.Average() / 5f : 0f; // normalize 0..1
                }

                // user-category affinity
                float uCatAff = 0f;
                if (catWeightByUser.TryGetValue(uid, out var map))
                {
                    var total = map.Values.Sum();
                    if (total > 0 && map.TryGetValue(e.CategoryId, out var catVal))
                        uCatAff = catVal / total;
                }

                // 30d engagement
                clicks30Cnt.TryGetValue(evtId, out var eCnt30);
                clicks30Dwell.TryGetValue(evtId, out var eDw30);

                // timing features
                var isUpcoming = e.DateTime >= now ? 1f : 0f;
                var daysTo = (float)(e.DateTime - now).TotalDays;
                if (daysTo < -7f) daysTo = -7f;
                if (daysTo > 60f) daysTo = 60f;

                var recency = RecencyScore(e.DateTime);
                var hour = (float)e.DateTime.Hour;
                var dow = (float)((int)e.DateTime.DayOfWeek);

                // MF score as a feature (learned blend)
                var mfScore = mfEngine.Predict(new MfRow { UserId = uid, EventId = evtId.ToString(), Label = 0f }).Score;

                rankRows.Add(new RankRow
                {
                    GroupId = uid,
                    Label = label,
                    EventRecency = recency,
                    OrganizerScore = orgScore,
                    VenueCapacity = capacity,
                    CategoryId = e.CategoryId,
                    HourOfDay = hour,
                    DayOfWeek = dow,
                    FriendsGoingRatio = frGoingRatio,
                    FriendsInterestedRatio = frInterestedRatio,
                    FriendsAvgRating = frAvgRating01,
                    UserCatAffinity = uCatAff,
                    EventClicks30d = eCnt30,
                    EventDwell30d = eDw30,
                    IsUpcoming = isUpcoming,
                    DaysToEvent = daysTo,
                    MFScore = mfScore
                });
            }

            foreach (var (uid, posSet) in byUserPosEvents)
            {
                var positives = posSet.ToList();
                if (positives.Count == 0) continue;

                var negPool = allEventIds.Except(positives).ToArray();
                var negSample = negPool
                    .OrderBy(_ => rnd.Next())
                    .Take(Math.Min(positives.Count * NEG_PER_POS_MULT, MAX_NEG_PER_USER))
                    .ToArray();

                foreach (var p in positives) AddRowsForUser(uid, p, 1f);
                foreach (var n in negSample) AddRowsForUser(uid, n, 0f);
            }

            var featureCols = new[]
            {
                nameof(RankRow.EventRecency),
                nameof(RankRow.OrganizerScore),
                nameof(RankRow.VenueCapacity),
                nameof(RankRow.CategoryId),
                nameof(RankRow.HourOfDay),
                nameof(RankRow.DayOfWeek),
                nameof(RankRow.FriendsGoingRatio),
                nameof(RankRow.FriendsInterestedRatio),
                nameof(RankRow.FriendsAvgRating),
                nameof(RankRow.UserCatAffinity),
                nameof(RankRow.EventClicks30d),
                nameof(RankRow.EventDwell30d),
                nameof(RankRow.IsUpcoming),
                nameof(RankRow.DaysToEvent),
                nameof(RankRow.MFScore)
            };

            var rankData = _ml.Data.LoadFromEnumerable(rankRows);

            var rankPipeline =
                _ml.Transforms.Conversion.MapValueToKey("GroupKey", nameof(RankRow.GroupId), addKeyValueAnnotationsAsText: true)
                  .Append(_ml.Transforms.Concatenate("Features", featureCols))
                  .Append(_ml.Ranking.Trainers.FastTree(new FastTreeRankingTrainer.Options
                  {
                      LabelColumnName = nameof(RankRow.Label),
                      FeatureColumnName = "Features",
                      RowGroupColumnName = "GroupKey",
                      NumberOfTrees = 120,
                      NumberOfLeaves = 32,
                      MinimumExampleCountPerLeaf = 10,
                      LearningRate = 0.18
                  }));

            var rankModel = rankPipeline.Fit(rankData);
            _ml.Model.Save(rankModel, rankData.Schema, _cfg.RankModelPath);
        }

        // -------------------- SERVE --------------------
        public async Task<int[]> RecommendForUserAsync(string userId, int topN = 10)
        {
            // Ultra-cold user fallback: few interactions and no social signals → use Trending
            var myCount = await _db.UserEventInteractions.CountAsync(i => i.UserId == userId);
            var hasFriends = await _db.Friendships.AnyAsync(f => f.FollowerId == userId);
            if (myCount < 2 && !hasFriends)
            {
                var (overall, byCat) = await _trending.GetTrendingForUserAsync(userId, topN);
                var catFirst = byCat.Values.SelectMany(x => x).ToList();
                var blended = catFirst.Concat(overall).Distinct().Take(topN).ToArray();
                return blended;
            }

            if (!ModelsExist()) return Array.Empty<int>();

            // Load models
            var mfModel = _ml.Model.Load(_cfg.MfModelPath, out _);
            var rankModel = _ml.Model.Load(_cfg.RankModelPath, out _);

            var mfEngine = _ml.Model.CreatePredictionEngine<MfRow, MfScore>(mfModel, ignoreMissingColumns: true);
            var rankEngine = _ml.Model.CreatePredictionEngine<RankRow, RankPrediction>(rankModel, ignoreMissingColumns: true);

            var now = DateTime.UtcNow;
            var cutoff = now.AddDays(-ENGAGEMENT_WINDOW_DAYS);

            // Candidate set = all events
            var eventsBasic = await _db.Events.AsNoTracking()
                .Select(e => new { e.EventId, e.CategoryId, e.VenueId, e.OrganizerId, e.DateTime })
                .ToListAsync();
            var eventById = eventsBasic.ToDictionary(x => x.EventId);

            var venueById = await _db.Venues.AsNoTracking().ToDictionaryAsync(v => v.VenueId, v => v);
            var organizerPop = await _db.Events.AsNoTracking()
                .GroupBy(e => e.OrganizerId)
                .Select(g => new { g.Key, Cnt = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => (float)x.Cnt);

            // friends of this user
            var friends = await _db.Friendships.AsNoTracking()
                .Where(f => f.FollowerId == userId)
                .Select(f => f.FolloweeId)
                .ToListAsync();
            var friendsSet = friends.ToHashSet();

            // interactions of this user (for category affinity)
            var myInters = await _db.UserEventInteractions.AsNoTracking()
                .Where(i => i.UserId == userId)
                .Select(i => new { i.EventId, i.Status, i.Rating })
                .ToListAsync();

            var catWeights = new Dictionary<int, float>();
            foreach (var i in myInters)
            {
                if (!eventById.TryGetValue(i.EventId, out var e)) continue;
                var w = Math.Max(
                    i.Status switch
                    {
                        InteractionStatus.Interested => _cfg.ScoreInterested,
                        InteractionStatus.Going => _cfg.ScoreGoing,
                        _ => 0f
                    },
                    i.Rating.HasValue ? _cfg.ScoreRated(i.Rating.Value) : 0f
                );
                if (w <= 0) continue;
                catWeights.TryGetValue(e.CategoryId, out var cur);
                catWeights[e.CategoryId] = cur + w;
            }
            var totalCat = catWeights.Values.Sum();

            // recent engagement
            var clicks30 = await _db.EventClicks.AsNoTracking()
                .Where(c => c.ClickedAt >= cutoff)
                .GroupBy(c => c.EventId)
                .Select(g => new
                {
                    EventId = g.Key,
                    Cnt = g.Count(),
                    Dwell = g.Sum(x => (double)(x.DwellMs ?? 0)) / 2000.0
                })
                .ToListAsync();
            var clicks30Cnt = clicks30.ToDictionary(x => x.EventId, x => (float)x.Cnt);
            var clicks30Dwell = clicks30.ToDictionary(x => x.EventId, x => (float)x.Dwell);

            // Precompute: totals per event (for ratios)
            var candidateIds = eventsBasic.Select(e => e.EventId).ToList();

            var totalsByEvent = await _db.UserEventInteractions.AsNoTracking()
                .Where(i => candidateIds.Contains(i.EventId))
                .GroupBy(i => i.EventId)
                .Select(g => new
                {
                    EventId = g.Key,
                    TotalGoing = g.Count(x => x.Status == InteractionStatus.Going),
                    TotalInterested = g.Count(x => x.Status == InteractionStatus.Interested)
                })
                .ToListAsync();
            var totalsDict = totalsByEvent.ToDictionary(x => x.EventId, x => (x.TotalGoing, x.TotalInterested));

            // friends' interactions across candidates
            var friendInters = await _db.UserEventInteractions.AsNoTracking()
                .Where(i => candidateIds.Contains(i.EventId) && friendsSet.Contains(i.UserId))
                .Select(i => new { i.EventId, i.Status, i.Rating })
                .ToListAsync();
            var friendIntersByEvent = friendInters.GroupBy(x => x.EventId).ToDictionary(g => g.Key, g => g.ToList());

            // Stage A: MF score → take top candidates
            var mfTop = candidateIds
                .Select(eid => new
                {
                    EventId = eid,
                    Score = mfEngine.Predict(new MfRow { UserId = userId, EventId = eid.ToString(), Label = 0 }).Score
                })
                .OrderByDescending(x => x.Score)
                .Take(_cfg.CandidatesPerUser)
                .Select(x => x.EventId)
                .ToList();

            if (mfTop.Count == 0) mfTop = candidateIds;

            float RecencyScore(DateTime dtUtc)
            {
                var daysAgo = (float)Math.Max(0, (now - dtUtc.ToUniversalTime()).TotalDays);
                return MathF.Exp(-daysAgo / 30f);
            }

            // Stage B: compute features per candidate and rank
            var ranked = mfTop
                .Select(eid =>
                {
                    if (!eventById.TryGetValue(eid, out var e)) return (eid, score: float.NegativeInfinity);

                    venueById.TryGetValue(e.VenueId, out var v);
                    var capacity = (float)(v?.Capacity ?? 0);

                    organizerPop.TryGetValue(e.OrganizerId, out var orgScore);

                    // social ratios
                    float frGoingRatio = 0f, frInterestedRatio = 0f, frAvgRating01 = 0f;
                    if (friendIntersByEvent.TryGetValue(eid, out var fi))
                    {
                        var frGoing = fi.Count(x => x.Status == InteractionStatus.Going);
                        var frInterested = fi.Count(x => x.Status == InteractionStatus.Interested);
                        var rs = fi.Where(x => x.Rating.HasValue).Select(x => x.Rating!.Value).ToList();
                        frAvgRating01 = rs.Count > 0 ? (float)rs.Average() / 5f : 0f;

                        if (totalsDict.TryGetValue(eid, out var totals))
                        {
                            frGoingRatio = totals.Item1 > 0 ? (float)frGoing / totals.Item1 : 0f;
                            frInterestedRatio = totals.Item2 > 0 ? (float)frInterested / totals.Item2 : 0f;
                        }
                    }

                    // affinity
                    float uCatAff = 0f;
                    if (totalCat > 0 && catWeights.TryGetValue(e.CategoryId, out var cat))
                        uCatAff = cat / totalCat;

                    // engagement
                    clicks30Cnt.TryGetValue(eid, out var eCnt30);
                    clicks30Dwell.TryGetValue(eid, out var eDw30);

                    // timing
                    var isUpcoming = e.DateTime >= now ? 1f : 0f;
                    var daysTo = (float)(e.DateTime - now).TotalDays;
                    if (daysTo < -7f) daysTo = -7f;
                    if (daysTo > 60f) daysTo = 60f;

                    var recency = RecencyScore(e.DateTime);
                    var hour = (float)e.DateTime.Hour;
                    var dow = (float)((int)e.DateTime.DayOfWeek);

                    // MF as feature
                    var mfScore = mfEngine.Predict(new MfRow { UserId = userId, EventId = eid.ToString(), Label = 0 }).Score;

                    var row = new RankRow
                    {
                        GroupId = userId,
                        Label = 0,
                        EventRecency = recency,
                        OrganizerScore = orgScore,
                        VenueCapacity = capacity,
                        CategoryId = e.CategoryId,
                        HourOfDay = hour,
                        DayOfWeek = dow,
                        FriendsGoingRatio = frGoingRatio,
                        FriendsInterestedRatio = frInterestedRatio,
                        FriendsAvgRating = frAvgRating01,
                        UserCatAffinity = uCatAff,
                        EventClicks30d = eCnt30,
                        EventDwell30d = eDw30,
                        IsUpcoming = isUpcoming,
                        DaysToEvent = daysTo,
                        MFScore = mfScore
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
    }
}
