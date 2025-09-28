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

            // Hydrate overall and preserve input order
            var overallEvents = await _db.Events.AsNoTracking()
                .Where(e => overallIds.Contains(e.EventId))
                .Include(e => e.Category).Include(e => e.Venue).Include(e => e.Organizer)
                .ToListAsync();

            var rankOverall = overallIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
            var overall = overallEvents
                .OrderBy(e => rankOverall[e.EventId])
                .Select(e => new EventDto(e))
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
                    Events = events.OrderBy(e => rank[e.EventId]).Select(e => new EventDto(e)).ToList()
                });
            }

            return Ok(new { overall, byCategory });
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
            public int? FriendsGoing { get; init; } // (optional) not computed here

            public EventDto(Event e)
            {
                Id = e.EventId;
                Title = e.Title;
                Description = e.Description;
                DateTime = e.DateTime;
                Location = e.Location;
                Category = e.Category?.Name ?? "";
                Venue = e.Venue?.Name ?? "";
                Organizer = e.Organizer?.Name ?? "";
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

