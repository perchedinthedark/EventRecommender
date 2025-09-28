using EventRecommender.Data;
using EventRecommender.Models;
using EventRecommender.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Trainers;
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
        private readonly MLContext _ml;
        private readonly ITrendingService _trending;

        private const int ENGAGEMENT_WINDOW_DAYS = 30;
        private const int NEG_PER_POS_MULT = 3;
        private const int MAX_NEG_PER_USER = 100;

        public RecommenderService(AppDbContext db, RecommenderConfig cfg, ITrendingService trending)
        {
            _db = db; _cfg = cfg; _trending = trending;
            _ml = new MLContext(seed: 42);
            Directory.CreateDirectory(_cfg.ModelDir);
        }

        public bool ModelsExist()
            => File.Exists(_cfg.MfModelPath) && File.Exists(_cfg.RankModelPath);

        // -------------------- TRAIN --------------------
        public async Task TrainAsync()
        {
            // 0) Pull signals
            var interactionsRaw = await _db.UserEventInteractions
                .AsNoTracking()
                .Select(i => new { i.UserId, i.EventId, i.Status, i.Rating, i.Timestamp })
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

            // 1) Stage A: MF training (keys inside the pipeline)
            var mfRows = prefs.Select(kv => new MfRow
            {
                UserId = kv.Key.uid,
                EventId = kv.Key.eid.ToString(),
                Label = kv.Value
            });

            var mfData = _ml.Data.LoadFromEnumerable(mfRows);

            var mfOptions = new MatrixFactorizationTrainer.Options
            {
                MatrixColumnIndexColumnName = "UserKey",
                MatrixRowIndexColumnName = "ItemKey",
                LabelColumnName = nameof(MfRow.Label),
                NumberOfIterations = 60,
                ApproximationRank = 64,
                Alpha = 0.01f,
                Lambda = 0.025f
            };

            var mfPipeline =
                _ml.Transforms.Conversion.MapValueToKey("UserKey", nameof(MfRow.UserId), addKeyValueAnnotationsAsText: true)
                  .Append(_ml.Transforms.Conversion.MapValueToKey("ItemKey", nameof(MfRow.EventId), addKeyValueAnnotationsAsText: true))
                  .Append(_ml.Recommendation().Trainers.MatrixFactorization(mfOptions));

            var mfModel = mfPipeline.Fit(mfData);
            _ml.Model.Save(mfModel, mfData.Schema, _cfg.MfModelPath);

            // Reuse MF for MFScore feature
            var mfEngine = _ml.Model.CreatePredictionEngine<MfRow, MfScore>(mfModel, ignoreMissingColumns: true);

            // 2) Feature materials
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

            // interactions for social/affinities
            var interAll = interactionsRaw; // already pulled
            var interByEvent = interAll.GroupBy(i => i.EventId).ToDictionary(g => g.Key, g => g.ToList());

            // user -> category weights
            var catWeightByUser = new Dictionary<string, Dictionary<int, float>>();
            // user -> organizer weights (familiarity)
            var orgWeightByUser = new Dictionary<string, Dictionary<int, float>>();
            // user -> hour/dow prefs
            var hourPrefByUser = new Dictionary<string, float[]>(); // 24
            var dowPrefByUser = new Dictionary<string, float[]>();  // 7

            foreach (var i in interAll)
            {
                if (!eventById.TryGetValue(i.EventId, out var eBase)) continue;
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

                // Cat affinity
                if (!catWeightByUser.TryGetValue(i.UserId, out var catMap))
                { catMap = new Dictionary<int, float>(); catWeightByUser[i.UserId] = catMap; }
                catMap.TryGetValue(eBase.CategoryId, out var cCur); catMap[eBase.CategoryId] = cCur + w;

                // Organizer prior
                if (!orgWeightByUser.TryGetValue(i.UserId, out var orgMap))
                { orgMap = new Dictionary<int, float>(); orgWeightByUser[i.UserId] = orgMap; }
                orgMap.TryGetValue(eBase.OrganizerId, out var oCur); orgMap[eBase.OrganizerId] = oCur + w;

                // Temporal prefs
                var hr = eBase.DateTime.Hour;
                var dw = (int)eBase.DateTime.DayOfWeek;
                if (!hourPrefByUser.TryGetValue(i.UserId, out var hrArr))
                { hrArr = new float[24]; hourPrefByUser[i.UserId] = hrArr; }
                if (!dowPrefByUser.TryGetValue(i.UserId, out var dwArr))
                { dwArr = new float[7]; dowPrefByUser[i.UserId] = dwArr; }
                hrArr[hr] += w;
                dwArr[dw] += w;
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

            // 3) Build ranking rows
            var byUserPosEvents = prefs
                .GroupBy(p => p.Key.uid)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Key.eid).ToHashSet());

            var allEventIds = eventsBasic.Select(e => e.EventId).ToArray();
            var rnd = new Random(123);

            float RecencyScore(DateTime dtUtc)
            {
                var daysAgo = (float)Math.Max(0, (now - dtUtc.ToUniversalTime()).TotalDays);
                return MathF.Exp(-daysAgo / 30f);
            }

            var rankRows = new List<RankRow>();

            void AddRowsForUser(string uid, int evtId, float label)
            {
                if (!eventById.TryGetValue(evtId, out var e)) return;

                venueById.TryGetValue(e.VenueId, out var venue);
                var capacity = (float)(venue?.Capacity ?? 0);

                organizerPop.TryGetValue(e.OrganizerId, out var orgScore);

                // Friends & totals (for rates)
                friendsMap.TryGetValue(uid, out var friends); friends ??= new HashSet<string>();
                int totalGoing = 0, totalInterested = 0, friendsGoing = 0, friendsInterested = 0;
                if (interByEvent.TryGetValue(evtId, out var evInters))
                {
                    foreach (var it in evInters)
                    {
                        if (it.Status == InteractionStatus.Going)
                        {
                            totalGoing++;
                            if (friends.Contains(it.UserId)) friendsGoing++;
                        }
                        else if (it.Status == InteractionStatus.Interested)
                        {
                            totalInterested++;
                            if (friends.Contains(it.UserId)) friendsInterested++;
                        }
                    }
                }
                var goingRate = totalGoing > 0 ? (float)friendsGoing / totalGoing : 0f;
                var interestedRate = totalInterested > 0 ? (float)friendsInterested / totalInterested : 0f;

                // User-category affinity
                float uCatAff = 0f;
                if (catWeightByUser.TryGetValue(uid, out var catMap))
                {
                    var total = catMap.Values.Sum();
                    if (total > 0 && catMap.TryGetValue(e.CategoryId, out var catVal))
                        uCatAff = catVal / total;
                }

                // Organizer familiarity for this user
                float orgPrior = 0f;
                if (orgWeightByUser.TryGetValue(uid, out var orgMap))
                {
                    var oSum = orgMap.Values.Sum();
                    if (oSum > 0 && orgMap.TryGetValue(e.OrganizerId, out var oVal))
                        orgPrior = oVal / oSum;
                }

                // Temporal affinities
                float hrAff = 0f, dowAff = 0f;
                if (hourPrefByUser.TryGetValue(uid, out var hrArr))
                {
                    var s = hrArr.Sum();
                    if (s > 0) hrAff = hrArr[e.DateTime.Hour] / s;
                }
                if (dowPrefByUser.TryGetValue(uid, out var dwArr))
                {
                    var s = dwArr.Sum();
                    if (s > 0) dowAff = dwArr[(int)e.DateTime.DayOfWeek] / s;
                }

                // Engagement in window
                clicks30Cnt.TryGetValue(evtId, out var eCnt30);
                clicks30Dwell.TryGetValue(evtId, out var eDw30);

                // Timing
                var isUpcoming = e.DateTime >= now ? 1f : 0f;
                var daysTo = (float)(e.DateTime - now).TotalDays;
                if (daysTo < -7f) daysTo = -7f;
                if (daysTo > 60f) daysTo = 60f;

                var recency = RecencyScore(e.DateTime);
                var hour = (float)e.DateTime.Hour;
                var dow = (float)((int)e.DateTime.DayOfWeek);

                // MF blend
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
                    FriendsGoingRate = goingRate,
                    FriendsInterestedRate = interestedRate,
                    UserCatAffinity = uCatAff,
                    UserHourAffinity = hrAff,
                    UserDowAffinity = dowAff,
                    OrganizerUserPrior = orgPrior,
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
                var negSample = negPool.OrderBy(_ => rnd.Next()).Take(Math.Min(positives.Count * NEG_PER_POS_MULT, MAX_NEG_PER_USER)).ToArray();

                foreach (var p in positives) AddRowsForUser(uid, p, 1f);
                foreach (var n in negSample) AddRowsForUser(uid, n, 0f);
            }

            var rankData = _ml.Data.LoadFromEnumerable(rankRows);

            var featureCols = new[]
            {
                nameof(RankRow.EventRecency),
                nameof(RankRow.OrganizerScore),
                nameof(RankRow.VenueCapacity),
                nameof(RankRow.CategoryId),
                nameof(RankRow.HourOfDay),
                nameof(RankRow.DayOfWeek),

                nameof(RankRow.FriendsGoingRate),
                nameof(RankRow.FriendsInterestedRate),

                nameof(RankRow.UserCatAffinity),
                nameof(RankRow.UserHourAffinity),
                nameof(RankRow.UserDowAffinity),
                nameof(RankRow.OrganizerUserPrior),

                nameof(RankRow.EventClicks30d),
                nameof(RankRow.EventDwell30d),

                nameof(RankRow.IsUpcoming),
                nameof(RankRow.DaysToEvent),

                nameof(RankRow.MFScore)
            };

            var rankPipeline =
                _ml.Transforms.Conversion.MapValueToKey("GroupKey", nameof(RankRow.GroupId), addKeyValueAnnotationsAsText: true)
                  .Append(_ml.Transforms.Concatenate("Features", featureCols))
                  .Append(_ml.Ranking.Trainers.FastTree(new FastTreeRankingTrainer.Options
                  {
                      LabelColumnName = nameof(RankRow.Label),
                      FeatureColumnName = "Features",
                      RowGroupColumnName = "GroupKey",
                      NumberOfTrees = 140,
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
            // Ultra-cold users → trending fallback
            var myCount = await _db.UserEventInteractions.CountAsync(i => i.UserId == userId);
            var hasFriends = await _db.Friendships.AnyAsync(f => f.FollowerId == userId);
            if (myCount < 2 && !hasFriends)
            {
                var (overall, byCat) = await _trending.GetTrendingForUserAsync(userId, topN);
                var blended = byCat.Values.SelectMany(x => x).Concat(overall).Distinct().Take(topN).ToArray();
                return blended;
            }

            if (!ModelsExist()) return Array.Empty<int>();

            // Load models
            DataViewSchema s1, s2;
            var mfModel = _ml.Model.Load(_cfg.MfModelPath, out s1);
            var rankModel = _ml.Model.Load(_cfg.RankModelPath, out s2);

            var mfEngine = _ml.Model.CreatePredictionEngine<MfRow, MfScore>(mfModel, ignoreMissingColumns: true);
            var rankEngine = _ml.Model.CreatePredictionEngine<RankRow, RankPrediction>(rankModel, ignoreMissingColumns: true);

            var now = DateTime.UtcNow;
            var cutoff = now.AddDays(-ENGAGEMENT_WINDOW_DAYS);

            // Candidates
            var eventsBasic = await _db.Events.AsNoTracking()
                .Select(e => new { e.EventId, e.CategoryId, e.VenueId, e.OrganizerId, e.DateTime })
                .ToListAsync();
            var eventById = eventsBasic.ToDictionary(x => x.EventId);

            var venueById = await _db.Venues.AsNoTracking().ToDictionaryAsync(v => v.VenueId, v => v);
            var organizerPop = await _db.Events.AsNoTracking()
                .GroupBy(e => e.OrganizerId)
                .Select(g => new { g.Key, Cnt = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => (float)x.Cnt);

            // Friends set
            var friends = await _db.Friendships.AsNoTracking()
                .Where(f => f.FollowerId == userId)
                .Select(f => f.FolloweeId)
                .ToListAsync();
            var friendsSet = friends.ToHashSet();

            // My interactions (for affinities & organizer prior & temporal prefs)
            var myInters = await _db.UserEventInteractions.AsNoTracking()
                .Where(i => i.UserId == userId)
                .Select(i => new { i.EventId, i.Status, i.Rating })
                .ToListAsync();

            var catWeights = new Dictionary<int, float>();
            var orgWeights = new Dictionary<int, float>();
            var hrArr = new float[24];
            var dwArr = new float[7];

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

                catWeights.TryGetValue(e.CategoryId, out var c); catWeights[e.CategoryId] = c + w;
                orgWeights.TryGetValue(e.OrganizerId, out var o); orgWeights[e.OrganizerId] = o + w;
                hrArr[e.DateTime.Hour] += w;
                dwArr[(int)e.DateTime.DayOfWeek] += w;
            }
            var catSum = catWeights.Values.Sum();
            var orgSum = orgWeights.Values.Sum();
            var hrSum = hrArr.Sum();
            var dwSum = dwArr.Sum();

            // Recent engagement
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

            // Social totals for candidate events (to compute rates)
            var candidateIds = eventsBasic.Select(e => e.EventId).ToList();
            var allIntersForCandidates = await _db.UserEventInteractions.AsNoTracking()
                .Where(i => candidateIds.Contains(i.EventId))
                .Select(i => new { i.UserId, i.EventId, i.Status, i.Rating })
                .ToListAsync();

            var totalsByEvent = new Dictionary<int, (int going, int interested)>();
            var friendIntersByEvent = new Dictionary<int, List<(InteractionStatus Status, int? Rating)>>();
            foreach (var i in allIntersForCandidates)
            {
                if (!totalsByEvent.TryGetValue(i.EventId, out var t)) t = (0, 0);
                if (i.Status == InteractionStatus.Going) t.going++;
                else if (i.Status == InteractionStatus.Interested) t.interested++;
                totalsByEvent[i.EventId] = t;

                if (friendsSet.Contains(i.UserId))
                {
                    if (!friendIntersByEvent.TryGetValue(i.EventId, out var list))
                    { list = new List<(InteractionStatus, int?)>(); friendIntersByEvent[i.EventId] = list; }
                    list.Add((i.Status, i.Rating));
                }
            }

            // Stage A: MF → top candidate pool
            var mfTop = candidateIds
                .Select(eid => new { EventId = eid, Score = mfEngine.Predict(new MfRow { UserId = userId, EventId = eid.ToString(), Label = 0 }).Score })
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

            // Stage B: feature compute + rank
            var ranked = mfTop
                .Select(eid =>
                {
                    if (!eventById.TryGetValue(eid, out var e)) return (eid, score: float.NegativeInfinity);

                    venueById.TryGetValue(e.VenueId, out var v);
                    var capacity = (float)(v?.Capacity ?? 0);

                    organizerPop.TryGetValue(e.OrganizerId, out var orgScore);

                    // Social rates
                    totalsByEvent.TryGetValue(eid, out var totals);
                    friendIntersByEvent.TryGetValue(eid, out var fints);
                    int fGoing = fints?.Count(x => x.Status == InteractionStatus.Going) ?? 0;
                    int fInterested = fints?.Count(x => x.Status == InteractionStatus.Interested) ?? 0;

                    var goingRate = totals.going > 0 ? (float)fGoing / totals.going : 0f;
                    var interestedRate = totals.interested > 0 ? (float)fInterested / totals.interested : 0f;

                    // Affinities
                    float uCatAff = (catSum > 0 && catWeights.TryGetValue(e.CategoryId, out var c)) ? c / catSum : 0f;
                    float orgPrior = (orgSum > 0 && orgWeights.TryGetValue(e.OrganizerId, out var o)) ? o / orgSum : 0f;
                    float hrAff = hrSum > 0 ? hrArr[e.DateTime.Hour] / hrSum : 0f;
                    float dwAff = dwSum > 0 ? dwArr[(int)e.DateTime.DayOfWeek] / dwSum : 0f;

                    // Engagement & timing
                    clicks30Cnt.TryGetValue(eid, out var eCnt30);
                    clicks30Dwell.TryGetValue(eid, out var eDw30);

                    var isUpcoming = e.DateTime >= now ? 1f : 0f;
                    var daysTo = (float)(e.DateTime - now).TotalDays;
                    if (daysTo < -7f) daysTo = -7f;
                    if (daysTo > 60f) daysTo = 60f;

                    var recency = RecencyScore(e.DateTime);
                    var hour = (float)e.DateTime.Hour;
                    var dow = (float)((int)e.DateTime.DayOfWeek);

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
                        FriendsGoingRate = goingRate,
                        FriendsInterestedRate = interestedRate,
                        UserCatAffinity = uCatAff,
                        UserHourAffinity = hrAff,
                        UserDowAffinity = dwAff,
                        OrganizerUserPrior = orgPrior,
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
