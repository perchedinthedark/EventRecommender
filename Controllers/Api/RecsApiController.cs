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
        { _svc = svc; _db = db; }

        // GET /api/recs?topN=6
        [HttpGet, Authorize]
        public async Task<IActionResult> Get([FromQuery] int topN = 6)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var ids = await _svc.RecommendForUserAsync(userId, topN);
            var events = await _db.Events.AsNoTracking()
                .Where(e => ids.Contains(e.EventId))
                .Include(e => e.Category).Include(e => e.Venue).Include(e => e.Organizer)
                .ToListAsync();

            // preserve ranking order
            var order = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
            var dto = events.OrderBy(e => order[e.EventId]).Select(e => new EventDto(e)).ToList();

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
            public EventDto(Models.Event e)
            {
                Id = e.EventId; Title = e.Title; Description = e.Description;
                DateTime = e.DateTime; Location = e.Location;
                Category = e.Category?.Name ?? ""; Venue = e.Venue?.Name ?? ""; Organizer = e.Organizer?.Name ?? "";
            }
        }
    }
}
