using EventRecommender.Data;
using EventRecommender.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EventRecommender.Controllers.Api
{
    [ApiController]
    [Route("api/events")]
    public class EventsApiController : ControllerBase
    {
        private readonly AppDbContext _db;
        public EventsApiController(AppDbContext db) { _db = db; }

        // POST /api/events/{id}/status
        [HttpPost("{id:int}/status"), Authorize, IgnoreAntiforgeryToken]
        public async Task<IActionResult> SetStatus(int id, [FromBody] StatusDto body)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (!Enum.TryParse<InteractionStatus>(body.Status, true, out var parsed))
                parsed = InteractionStatus.None;

            var row = await _db.UserEventInteractions
                .FirstOrDefaultAsync(x => x.UserId == userId && x.EventId == id);

            if (row == null)
                _db.UserEventInteractions.Add(new UserEventInteraction { UserId = userId, EventId = id, Status = parsed, Timestamp = DateTime.UtcNow });
            else { row.Status = parsed; row.Timestamp = DateTime.UtcNow; }

            await _db.SaveChangesAsync();
            return Ok(new { ok = true, status = parsed.ToString() });
        }

        // POST /api/events/{id}/rating
        [HttpPost("{id:int}/rating"), Authorize, IgnoreAntiforgeryToken]
        public async Task<IActionResult> SetRating(int id, [FromBody] RatingDto body)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            if (body.Rating < 1 || body.Rating > 5) return BadRequest("rating 1..5");

            var row = await _db.UserEventInteractions
                .FirstOrDefaultAsync(x => x.UserId == userId && x.EventId == id);

            if (row == null)
                _db.UserEventInteractions.Add(new UserEventInteraction { UserId = userId, EventId = id, Rating = body.Rating, Timestamp = DateTime.UtcNow });
            else { row.Rating = body.Rating; row.Timestamp = DateTime.UtcNow; }

            await _db.SaveChangesAsync();
            return Ok(new { ok = true, rating = body.Rating });
        }

        // GET /api/events/{id}/me
        [HttpGet("{id:int}/me"), Authorize]
        public async Task<IActionResult> GetMine(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var row = await _db.UserEventInteractions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.EventId == id);

            var status = row?.Status.ToString() ?? "None";
            var rating = row?.Rating;

            return Ok(new { status, rating });
        }

        // GET /api/events/{id} (includes AvgRating)
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetEvent(int id)
        {
            var e = await _db.Events.AsNoTracking()
                .Include(x => x.Category).Include(x => x.Venue).Include(x => x.Organizer)
                .FirstOrDefaultAsync(x => x.EventId == id);
            if (e == null) return NotFound();

            double? avgRating = await _db.UserEventInteractions.AsNoTracking()
                .Where(i => i.EventId == id && i.Rating.HasValue)
                .Select(i => (double?)i.Rating)
                .AverageAsync();

            return Ok(new
            {
                id = e.EventId,
                title = e.Title,
                description = e.Description,
                dateTime = e.DateTime,
                location = e.Location,
                category = e.Category?.Name ?? "",
                venue = e.Venue?.Name ?? "",
                organizer = e.Organizer?.Name ?? "",
                avgRating
            });
        }

        // NEW: GET /api/events/mine?status=Interested|Going
        [HttpGet("mine"), Authorize]
        public async Task<IActionResult> Mine([FromQuery] string status, [FromQuery] int topN = 200)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            if (!Enum.TryParse<InteractionStatus>(status, true, out var parsed) || parsed == InteractionStatus.None)
                return BadRequest("status must be Interested or Going");

            var eventIds = await _db.UserEventInteractions.AsNoTracking()
                .Where(i => i.UserId == userId && i.Status == parsed)
                .OrderByDescending(i => i.Timestamp)
                .Select(i => i.EventId)
                .Take(topN)
                .ToListAsync();

            var avgRatings = await _db.UserEventInteractions.AsNoTracking()
                .Where(i => eventIds.Contains(i.EventId) && i.Rating.HasValue)
                .GroupBy(i => i.EventId)
                .Select(g => new { EventId = g.Key, Avg = g.Average(x => x.Rating!.Value) })
                .ToDictionaryAsync(x => x.EventId, x => (double?)x.Avg);

            var events = await _db.Events.AsNoTracking()
                .Where(e => eventIds.Contains(e.EventId))
                .Include(e => e.Category).Include(e => e.Venue).Include(e => e.Organizer)
                .ToListAsync();

            var order = eventIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
            var dto = events.OrderBy(e => order[e.EventId])
                .Select(e => new
                {
                    id = e.EventId,
                    title = e.Title,
                    description = e.Description,
                    dateTime = e.DateTime,
                    location = e.Location,
                    category = e.Category!.Name,
                    venue = e.Venue!.Name,
                    organizer = e.Organizer!.Name,
                    avgRating = avgRatings.TryGetValue(e.EventId, out var a) ? a : null
                }).ToList();

            return Ok(dto);
        }

        public record StatusDto(string Status);
        public record RatingDto(int Rating);
    }
}

