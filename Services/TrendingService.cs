using EventRecommender.Data;
using EventRecommender.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EventRecommender.Services
{
    public interface ITrendingService
    {
        Task<List<int>> GetTrendingOverallAsync(int topN);
        Task<List<int>> GetTrendingByCategoryAsync(int categoryId, int topN);
        Task<(List<int> overall, Dictionary<int, List<int>> byCategory)> GetTrendingForUserAsync(string? userId, int perList, int categoriesToShow = 2);
    }

    public sealed class TrendingService : ITrendingService
    {
        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
        private const int WINDOW_DAYS = 30;
        private const int UPCOMING_SOON_DAYS = 14;

        public TrendingService(AppDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<List<int>> GetTrendingOverallAsync(int topN)
        {
            var key = $"trending:overall:{topN}";
            if (_cache.TryGetValue(key, out List<int>? cached) && cached is not null) return cached;

            var scores = await ComputeScoresAsync();
            var ids = scores.OrderByDescending(kv => kv.Value).Take(topN).Select(kv => kv.Key).ToList();
            _cache.Set(key, ids, CacheTtl);
            return ids;
        }

        public async Task<List<int>> GetTrendingByCategoryAsync(int categoryId, int topN)
        {
            var key = $"trending:cat:{categoryId}:{topN}";
            if (_cache.TryGetValue(key, out List<int>? cached) && cached is not null) return cached;

            var scores = await ComputeScoresAsync();
            var catIds = await _db.Events.AsNoTracking()
                .Where(e => e.CategoryId == categoryId)
                .Select(e => e.EventId)
                .ToListAsync();

            var ids = catIds
                .Where(id => scores.ContainsKey(id))
                .OrderByDescending(id => scores[id])
                .Take(topN)
                .ToList();

            _cache.Set(key, ids, CacheTtl);
            return ids;
        }

        public async Task<(List<int> overall, Dictionary<int, List<int>> byCategory)> GetTrendingForUserAsync(string? userId, int perList, int categoriesToShow = 2)
        {
            var overall = await GetTrendingOverallAsync(perList);

            // pick user's top categories (simple sum of weights from interactions, all-time)
            var byCategory = new Dictionary<int, List<int>>();
            if (!string.IsNullOrEmpty(userId))
            {
                var my = await _db.UserEventInteractions.AsNoTracking()
                    .Where(i => i.UserId == userId)
                    .Select(i => new { i.EventId, i.Status, i.Rating })
                    .ToListAsync();

                if (my.Count > 0)
                {
                    var ev = await _db.Events.AsNoTracking()
                        .Where(e => my.Select(m => m.EventId).Contains(e.EventId))
                        .Select(e => new { e.EventId, e.CategoryId })
                        .ToListAsync();

                    var catWeights = new Dictionary<int, float>();
                    foreach (var m in my)
                    {
                        var catId = ev.FirstOrDefault(x => x.EventId == m.EventId)?.CategoryId;
                        if (catId == null) continue;

                        float w = Math.Max(
                            m.Status == InteractionStatus.Interested ? 0.7f :
                            m.Status == InteractionStatus.Going ? 1.0f : 0f,
                            m.Rating.HasValue ? (m.Rating!.Value switch
                            {
                                <= 1 => 0.4f,
                                2 => 0.55f,
                                3 => 0.7f,
                                4 => 0.85f,
                                _ => 1.0f
                            }) : 0f);

                        if (w <= 0) continue;
                        catWeights.TryGetValue(catId.Value, out var cur);
                        catWeights[catId.Value] = cur + w;
                    }

                    foreach (var catId in catWeights
                        .OrderByDescending(kv => kv.Value)
                        .Take(Math.Max(1, categoriesToShow))
                        .Select(kv => kv.Key))
                    {
                        byCategory[catId] = await GetTrendingByCategoryAsync(catId, perList);
                    }
                }
            }

            return (overall, byCategory);
        }

        // ---------- scoring ----------
        private async Task<Dictionary<int, double>> ComputeScoresAsync()
        {
            var now = DateTime.UtcNow;
            var cutoff = now.AddDays(-WINDOW_DAYS);

            // basic event info
            var events = await _db.Events.AsNoTracking()
                .Select(e => new { e.EventId, e.CategoryId, e.DateTime })
                .ToListAsync();
            var eDict = events.ToDictionary(e => e.EventId, e => e);

            // clicks (30d)
            var clicks = await _db.EventClicks.AsNoTracking()
                .Where(c => c.ClickedAt >= cutoff)
                .ToListAsync();
            var clicksByEvent = clicks
                .GroupBy(c => c.EventId)
                .ToDictionary(g => g.Key, g => new { Cnt = g.Count(), Dwell = g.Sum(x => (double)(x.DwellMs ?? 0) / 2000.0) });

            // interactions (30d)
            var inters = await _db.UserEventInteractions.AsNoTracking()
                .Where(i => i.Timestamp >= cutoff)
                .ToListAsync();
            var interByEvent = inters.GroupBy(i => i.EventId).ToDictionary(g => g.Key, g => g.ToList());

            // global rating prior (30d)
            var allRatings = inters.Where(i => i.Rating.HasValue).Select(i => i.Rating!.Value).ToList();
            var priorMean = allRatings.Count > 0 ? allRatings.Average() : 3.5; // neutral-ish
            const double priorStrength = 3.0; // pseudo-count

            double Recency(DateTime startUtc)
            {
                var daysOld = Math.Max(0.0, (now - startUtc.ToUniversalTime()).TotalDays);
                return Math.Exp(-daysOld / 14.0); // 2-week half-life-ish
            }

            double UpcomingBoost(DateTime startUtc)
            {
                var daysTo = (startUtc - now).TotalDays;
                if (daysTo < 0) return 0.0;
                if (daysTo > UPCOMING_SOON_DAYS) return Math.Exp(-(daysTo - UPCOMING_SOON_DAYS) / 14.0);
                return 1.0; // strong boost if within soon window
            }

            var scores = new Dictionary<int, double>();

            foreach (var e in events)
            {
                clicksByEvent.TryGetValue(e.EventId, out var c);
                interByEvent.TryGetValue(e.EventId, out var li);
                var interested = li?.Count(x => x.Status == InteractionStatus.Interested) ?? 0;
                var going = li?.Count(x => x.Status == InteractionStatus.Going) ?? 0;

                var rList = li?.Where(x => x.Rating.HasValue).Select(x => (double)x.Rating!.Value).ToList() ?? new List<double>();
                var n = rList.Count;
                var avg = n > 0 ? rList.Average() : priorMean;
                // Bayesian smoothing to avoid small-n spikes
                var bayes = ((priorStrength * priorMean) + (n * avg)) / (priorStrength + n);

                // combine (weights: tweak later as needed)
                var score =
                      0.6 * Math.Sqrt(c?.Cnt ?? 0)
                    + 0.6 * (c?.Dwell ?? 0.0)
                    + 0.8 * interested
                    + 1.0 * going
                    + 0.5 * bayes
                    + 0.6 * Recency(e.DateTime)
                    + 0.6 * UpcomingBoost(e.DateTime);

                scores[e.EventId] = score;
            }

            return scores;
        }
    }
}
