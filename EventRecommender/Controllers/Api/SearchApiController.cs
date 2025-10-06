using System.Security.Claims;
using EventRecommender.Data;
using EventRecommender.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventRecommender.Controllers.Api
{
    [ApiController]
    [Route("api/search")]
    public class SearchApiController : ControllerBase
    {
        private readonly AppDbContext _db;

        public SearchApiController(AppDbContext db)
        {
            _db = db;
        }

        // GET /api/search?q=...&topN=50
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int topN = 50)
        {
            q = (q ?? "").Trim();
            topN = Math.Clamp(topN, 1, 200);

            if (string.IsNullOrWhiteSpace(q))
                return Ok(Array.Empty<object>());

            // tokenize
            var tokens = q.ToLowerInvariant()
                          .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                          .Distinct()
                          .ToArray();
            if (tokens.Length == 0)
                return Ok(Array.Empty<object>());

            var patterns = tokens.Select(t => $"%{t}%").ToArray();

            // base query with joins
            var baseQ = _db.Events
                .AsNoTracking()
                .Include(e => e.Category)
                .Include(e => e.Venue)
                .Include(e => e.Organizer);

            // --- Fix: query per token, union IDs in memory (avoid EF translating tokens.Any(...) across joins) ---
            var idSet = new HashSet<int>();
            foreach (var p in patterns)
            {
                var ids = await baseQ
                    .Where(e =>
                        (e.Title != null && EF.Functions.Like(e.Title, p)) ||
                        (e.Description != null && EF.Functions.Like(e.Description, p)) ||
                        (e.Location != null && EF.Functions.Like(e.Location, p)) ||
                        (e.Category != null && EF.Functions.Like(e.Category.Name, p)) ||
                        (e.Venue != null && EF.Functions.Like(e.Venue.Name, p)) ||
                        (e.Organizer != null && EF.Functions.Like(e.Organizer.Name, p))
                    )
                    .Select(e => e.EventId)
                    .ToListAsync();

                foreach (var id in ids)
                    idSet.Add(id);
            }

            if (idSet.Count == 0)
                return Ok(Array.Empty<object>());

            // load candidates
            var candidates = await baseQ
                .Where(e => idSet.Contains(e.EventId))
                .ToListAsync();

            // recent signals window
            var now = DateTime.UtcNow;
            var cutoff = now.AddDays(-30);
            var candIds = candidates.Select(e => e.EventId).ToList();

            // clicks/dwell
            var clicks = await _db.EventClicks.AsNoTracking()
                .Where(c => candIds.Contains(c.EventId) && c.ClickedAt >= cutoff)
                .GroupBy(c => c.EventId)
                .Select(g => new
                {
                    EventId = g.Key,
                    Cnt = g.Count(),
                    Dwell = g.Sum(x => (double)(x.DwellMs ?? 0)) / 2000.0
                })
                .ToListAsync();
            var clicksCnt = clicks.ToDictionary(x => x.EventId, x => (float)x.Cnt);
            var clicksDwell = clicks.ToDictionary(x => x.EventId, x => (float)x.Dwell);

            // interactions (for going/interested)
            var inters = await _db.UserEventInteractions.AsNoTracking()
                .Where(i => candIds.Contains(i.EventId) && i.Timestamp >= cutoff)
                .ToListAsync();
            var interByEvent = inters.GroupBy(i => i.EventId).ToDictionary(g => g.Key, g => g.ToList());

            // avg rating per event
            var avgRatings = await _db.UserEventInteractions.AsNoTracking()
                .Where(i => candIds.Contains(i.EventId) && i.Rating.HasValue)
                .GroupBy(i => i.EventId)
                .Select(g => new { EventId = g.Key, Avg = g.Average(x => x.Rating!.Value) })
                .ToDictionaryAsync(x => x.EventId, x => (double?)x.Avg);

            // user affinities (optional if logged-in)
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var catAff = new Dictionary<int, float>();
            var orgAff = new Dictionary<int, float>();
            if (!string.IsNullOrEmpty(uid))
            {
                var mine = await _db.UserEventInteractions.AsNoTracking()
                    .Where(i => i.UserId == uid)
                    .Select(i => new { i.EventId, i.Status, i.Rating })
                    .ToListAsync();

                var eventBasics = await _db.Events.AsNoTracking()
                    .Where(e => mine.Select(m => m.EventId).Contains(e.EventId))
                    .Select(e => new { e.EventId, e.CategoryId, e.OrganizerId })
                    .ToListAsync();
                var byId = eventBasics.ToDictionary(x => x.EventId, x => x);

                foreach (var m in mine)
                {
                    if (!byId.TryGetValue(m.EventId, out var b)) continue;

                    float w = Math.Max(
                        m.Status == InteractionStatus.Interested ? 0.7f :
                        m.Status == InteractionStatus.Going ? 1.0f : 0f,
                        m.Rating.HasValue
                            ? (m.Rating!.Value <= 1 ? 0.4f :
                               m.Rating!.Value == 2 ? 0.55f :
                               m.Rating!.Value == 3 ? 0.7f :
                               m.Rating!.Value == 4 ? 0.85f : 1.0f)
                            : 0f);

                    if (w <= 0) continue;

                    catAff.TryGetValue(b.CategoryId, out var ccur);
                    catAff[b.CategoryId] = ccur + w;

                    orgAff.TryGetValue(b.OrganizerId, out var ocur);
                    orgAff[b.OrganizerId] = ocur + w;
                }

                var csum = catAff.Values.Sum();
                var osum = orgAff.Values.Sum();
                if (csum > 0) foreach (var k in catAff.Keys.ToList()) catAff[k] = catAff[k] / csum;
                if (osum > 0) foreach (var k in orgAff.Keys.ToList()) orgAff[k] = orgAff[k] / osum;
            }

            // scoring
            float ScoreEvent(Event e)
            {
                var title = (e.Title ?? "").ToLowerInvariant();
                var desc = (e.Description ?? "").ToLowerInvariant();
                var cat = e.Category?.Name?.ToLowerInvariant() ?? "";
                var ven = e.Venue?.Name?.ToLowerInvariant() ?? "";
                var org = e.Organizer?.Name?.ToLowerInvariant() ?? "";
                var loc = e.Location?.ToLowerInvariant() ?? "";

                float text = 0f;
                foreach (var t in tokens)
                {
                    if (title.Contains(t)) text += 3.0f;
                    if (!string.IsNullOrEmpty(desc) && desc.Contains(t)) text += 1.2f;
                    if (!string.IsNullOrEmpty(cat) && cat.Contains(t)) text += 1.0f;
                    if (!string.IsNullOrEmpty(org) && org.Contains(t)) text += 0.9f;
                    if (!string.IsNullOrEmpty(ven) && ven.Contains(t)) text += 0.7f;
                    if (!string.IsNullOrEmpty(loc) && loc.Contains(t)) text += 0.5f;
                }

                var daysAgo = Math.Max(0.0, (DateTime.UtcNow - e.DateTime.ToUniversalTime()).TotalDays);
                var recency = (float)Math.Exp(-daysAgo / 30.0);

                clicksCnt.TryGetValue(e.EventId, out var cCnt);
                clicksDwell.TryGetValue(e.EventId, out var cDw);
                interByEvent.TryGetValue(e.EventId, out var evInters);
                var interested = evInters?.Count(x => x.Status == InteractionStatus.Interested) ?? 0;
                var going = evInters?.Count(x => x.Status == InteractionStatus.Going) ?? 0;

                float engagement = 0.55f * MathF.Sqrt(cCnt)
                                 + 0.55f * (cDw)
                                 + 0.6f * interested
                                 + 0.75f * going;

                float aff = 0f;
                if (catAff.Count > 0)
                {
                    if (catAff.TryGetValue(e.CategoryId, out var ca)) aff += 0.6f * ca;
                    if (orgAff.TryGetValue(e.OrganizerId, out var oa)) aff += 0.4f * oa;
                }

                return 1.0f * text + 0.6f * engagement + 0.5f * recency + 0.8f * aff;
            }

            var ranked = candidates
                .Select(e => new { e, s = ScoreEvent(e) })
                .OrderByDescending(x => x.s)
                .Take(topN)
                .Select(x => x.e)
                .ToList();

            var dto = ranked.Select(e => new
            {
                id = e.EventId,
                title = e.Title,
                description = e.Description,
                dateTime = e.DateTime,
                location = e.Location,
                category = e.Category?.Name ?? "",
                venue = e.Venue?.Name ?? "",
                organizer = e.Organizer?.Name ?? "",
                imageUrl = e.ImageUrl,
                avgRating = avgRatings.TryGetValue(e.EventId, out var a) ? a : null
            });

            return Ok(dto);
        }
    }
}
