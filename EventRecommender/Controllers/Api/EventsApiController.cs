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

        // POST /api/events/{id}/status { status: "Interested" | "Going" }
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

        // POST /api/events/{id}/rating { rating: 1..5 }
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


        // GET /api/events/{id}
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetEvent(int id)
        {
            var e = await _db.Events.AsNoTracking()
                .Include(x => x.Category).Include(x => x.Venue).Include(x => x.Organizer)
                .FirstOrDefaultAsync(x => x.EventId == id);
            if (e == null) return NotFound();

            return Ok(new
            {
                id = e.EventId,
                title = e.Title,
                description = e.Description,
                dateTime = e.DateTime,
                location = e.Location,
                category = e.Category?.Name ?? "",
                venue = e.Venue?.Name ?? "",
                organizer = e.Organizer?.Name ?? ""
            });
        }


        public record StatusDto(string Status);
        public record RatingDto(int Rating);
    }
}
