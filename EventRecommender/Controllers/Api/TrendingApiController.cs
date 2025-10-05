using EventRecommender.Data;
using EventRecommender.Models;
using EventRecommender.Services;
using Microsoft.AspNetCore.Authorization;
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
        {
            _trending = trending;
            _db = db;
        }

        // GET /api/trending?perList=6&categoriesToShow=2
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Get([FromQuery] int perList = 6, [FromQuery] int categoriesToShow = 2)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier); // null if not signed in

            var (overallIds, byCatIds) = await _trending.GetTrendingForUserAsync(uid, perList, categoriesToShow);

            // union all ids for single avg-rating query
            var allIds = overallIds.Concat(byCatIds.SelectMany(kv => kv.Value)).Distinct().ToList();
            var avgRatings = await _db.UserEventInteractions.AsNoTracking()
                .Where(i => allIds.Contains(i.EventId) && i.Rating.HasValue)
                .GroupBy(i => i.EventId)
                .Select(g => new { EventId = g.Key, Avg = g.Average(x => x.Rating!.Value) })
                .ToDictionaryAsync(x => x.EventId, x => (double?)x.Avg);

            // Hydrate overall and preserve input order
            var overallEvents = await _db.Events.AsNoTracking()
                .Where(e => overallIds.Contains(e.EventId))
                .Include(e => e.Category).Include(e => e.Venue).Include(e => e.Organizer)
                .ToListAsync();

            var rankOverall = overallIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
            var overall = overallEvents
                .OrderBy(e => rankOverall[e.EventId])
                .Select(e => new EventDto(e, avgRatings.TryGetValue(e.EventId, out var a) ? a : null))
                .ToList();

            // Hydrate by-category blocks
            var catNames = await _db.Categories.AsNoTracking()
                .ToDictionaryAsync(c => c.CategoryId, c => c.Name);

            var byCategory = new List<CategoryBlockDto>();
            foreach (var kv in byCatIds)
            {
                var ids = kv.Value;
                var events = await _db.Events.AsNoTracking()
                    .Where(e => ids.Contains(e.EventId))
                    .Include(e => e.Category).Include(e => e.Venue).Include(e => e.Organizer)
                    .ToListAsync();

                var rank = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
                byCategory.Add(new CategoryBlockDto
                {
                    CategoryId = kv.Key,
                    CategoryName = catNames.TryGetValue(kv.Key, out var nm) ? nm : $"Category {kv.Key}",
                    Events = events.OrderBy(e => rank[e.EventId])
                                   .Select(e => new EventDto(e, avgRatings.TryGetValue(e.EventId, out var a) ? a : null))
                                   .ToList()
                });
            }

            return Ok(new { overall, byCategory });
        }

        // NEW: GET /api/trending/by-category?categoryId=##&topN=50
        [HttpGet("by-category")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByCategory([FromQuery] int categoryId, [FromQuery] int topN = 50)
        {
            if (categoryId <= 0) return BadRequest("categoryId required");

            var ids = await _trending.GetTrendingByCategoryAsync(categoryId, topN);

            var avgRatings = await _db.UserEventInteractions.AsNoTracking()
                .Where(i => ids.Contains(i.EventId) && i.Rating.HasValue)
                .GroupBy(i => i.EventId)
                .Select(g => new { EventId = g.Key, Avg = g.Average(x => x.Rating!.Value) })
                .ToDictionaryAsync(x => x.EventId, x => (double?)x.Avg);

            var events = await _db.Events.AsNoTracking()
                .Where(e => ids.Contains(e.EventId))
                .Include(e => e.Category).Include(e => e.Venue).Include(e => e.Organizer)
                .ToListAsync();

            var order = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
            var dto = events.OrderBy(e => order[e.EventId])
                .Select(e => new EventDto(e, avgRatings.TryGetValue(e.EventId, out var a) ? a : null))
                .ToList();

            var name = await _db.Categories.AsNoTracking()
                .Where(c => c.CategoryId == categoryId).Select(c => c.Name).FirstOrDefaultAsync() ?? $"Category {categoryId}";

            return Ok(new { categoryId, categoryName = name, events = dto });
        }

        public record EventDto
        {
            public int Id { get; init; }
            public string Title { get; init; } = "";
            public string? Description { get; init; }
            public DateTime DateTime { get; init; }
            public string? Location { get; init; }
            public string Category { get; init; } = "";
            public string Venue { get; init; } = "";
            public string Organizer { get; init; } = "";
            public string? ImageUrl { get; init; }
            public double? AvgRating { get; init; }
            public int? FriendsGoing { get; init; } // (optional) not computed here

            public EventDto(Event e, double? avgRating)
            {
                Id = e.EventId;
                Title = e.Title;
                Description = e.Description;
                DateTime = e.DateTime;
                Location = e.Location;
                Category = e.Category?.Name ?? "";
                Venue = e.Venue?.Name ?? "";
                Organizer = e.Organizer?.Name ?? "";
                ImageUrl = e.ImageUrl;
                AvgRating = avgRating;
            }
        }

        public record CategoryBlockDto
        {
            public int CategoryId { get; init; }
            public string CategoryName { get; init; } = "";
            public List<EventDto> Events { get; init; } = new();
        }
    }
}


