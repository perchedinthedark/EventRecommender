using EventRecommender.Data;
using EventRecommender.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EventRecommender.Controllers.Api
{
    [ApiController]
    [Route("api/trending")]
    public class TrendingApiController : ControllerBase
    {
        private readonly ITrendingService _trending;
        private readonly AppDbContext _db;

        public TrendingApiController(ITrendingService trending, AppDbContext db)
        { _trending = trending; _db = db; }

        // GET /api/trending/overall?topN=6
        [HttpGet("overall")]
        public async Task<IActionResult> Overall([FromQuery] int topN = 6)
        {
            var uid = User.Identity?.IsAuthenticated == true
                ? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                : null;

            var (overallIds, _) = await _trending.GetTrendingForUserAsync(uid, topN, categoriesToShow: 0);

            var events = await _db.Events.AsNoTracking()
                .Where(e => overallIds.Contains(e.EventId))
                .Include(e => e.Category).Include(e => e.Venue).Include(e => e.Organizer)
                .ToListAsync();

            var order = overallIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
            var dto = events.OrderBy(e => order[e.EventId]).Select(e => new SimpleEventDto(e)).ToList();
            return Ok(dto);
        }

        // GET /api/trending/by-category?perList=6&categoriesToShow=2
        [HttpGet("by-category")]
        public async Task<IActionResult> ByCategory([FromQuery] int perList = 6, [FromQuery] int categoriesToShow = 2)
        {
            var uid = User.Identity?.IsAuthenticated == true
                ? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                : null;

            var (_, byCat) = await _trending.GetTrendingForUserAsync(uid, perList, categoriesToShow);

            var catNames = await _db.Categories.AsNoTracking()
                .ToDictionaryAsync(c => c.CategoryId, c => c.Name);

            var result = new List<object>();
            foreach (var kv in byCat)
            {
                var ids = kv.Value;
                var evs = await _db.Events.AsNoTracking()
                    .Where(e => ids.Contains(e.EventId))
                    .Include(e => e.Category).Include(e => e.Venue).Include(e => e.Organizer)
                    .ToListAsync();

                var order = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
                result.Add(new
                {
                    categoryId = kv.Key,
                    categoryName = catNames.TryGetValue(kv.Key, out var n) ? n : $"Category {kv.Key}",
                    events = evs.OrderBy(e => order[e.EventId]).Select(e => new SimpleEventDto(e)).ToList()
                });
            }
            return Ok(result);
        }

        public record SimpleEventDto
        {
            public int Id { get; init; }
            public string Title { get; init; } = "";
            public DateTime DateTime { get; init; }
            public string Category { get; init; } = "";
            public string Venue { get; init; } = "";
            public string Organizer { get; init; } = "";
            public SimpleEventDto(Models.Event e)
            {
                Id = e.EventId; Title = e.Title; DateTime = e.DateTime;
                Category = e.Category?.Name ?? ""; Venue = e.Venue?.Name ?? ""; Organizer = e.Organizer?.Name ?? "";
            }
        }
    }
}
