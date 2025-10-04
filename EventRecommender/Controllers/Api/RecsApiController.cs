using EventRecommender.Data;
using EventRecommender.ML;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EventRecommender.Controllers.Api
{
    [ApiController]
    [Route("api/recs")]
    public class RecsApiController : ControllerBase
    {
        private readonly IRecommenderService _svc;
        private readonly AppDbContext _db;

        public RecsApiController(IRecommenderService svc, AppDbContext db)
        {
            _svc = svc;
            _db = db;
        }

        // GET /api/recs?topN=6
        // GET /api/recs/top?topN=6
        [HttpGet, Authorize]
        [HttpGet("top"), Authorize]
        public async Task<IActionResult> Get([FromQuery] int topN = 6)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var ids = await _svc.RecommendForUserAsync(userId, topN);

            var events = await _db.Events.AsNoTracking()
                .Where(e => ids.Contains(e.EventId))
                .Include(e => e.Category)
                .Include(e => e.Venue)
                .Include(e => e.Organizer)
                .ToListAsync();

            // avg ratings for these ids (single query)
            var avgRatings = await _db.UserEventInteractions.AsNoTracking()
                .Where(i => ids.Contains(i.EventId) && i.Rating.HasValue)
                .GroupBy(i => i.EventId)
                .Select(g => new { EventId = g.Key, Avg = g.Average(x => x.Rating!.Value) })
                .ToDictionaryAsync(x => x.EventId, x => (double?)x.Avg);

            // Preserve model ranking order
            var order = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);

            var dto = events
                .OrderBy(e => order.TryGetValue(e.EventId, out var idx) ? idx : int.MaxValue)
                .Select(e => new EventDto(e, avgRatings.TryGetValue(e.EventId, out var a) ? a : null))
                .ToList();

            return Ok(dto);
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
            public double? AvgRating { get; init; }

            public EventDto(Models.Event e, double? avgRating)
            {
                Id = e.EventId;
                Title = e.Title;
                Description = e.Description;
                DateTime = e.DateTime;
                Location = e.Location;
                Category = e.Category?.Name ?? "";
                Venue = e.Venue?.Name ?? "";
                Organizer = e.Organizer?.Name ?? "";
                AvgRating = avgRating;
            }
        }
    }
}
