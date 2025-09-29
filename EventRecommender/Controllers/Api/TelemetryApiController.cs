using EventRecommender.Data;
using EventRecommender.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EventRecommender.Controllers.Api
{
    [ApiController]
    [Route("api/telemetry")]
    public class TelemetryApiController : ControllerBase
    {
        private readonly AppDbContext _db;
        public TelemetryApiController(AppDbContext db) { _db = db; }

        // POST /api/telemetry/clicks { eventId }
        [HttpPost("clicks")]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> RecordClick([FromBody] ClickDto dto)
        {
            if (dto == null || dto.EventId <= 0) return BadRequest();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // may be null
            _db.EventClicks.Add(new EventClick { UserId = userId, EventId = dto.EventId, ClickedAt = DateTime.UtcNow });
            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }

        public record ClickDto(int EventId);

        // POST /api/telemetry/dwell { eventId, dwellMs }
        [HttpPost("dwell")]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> RecordDwell([FromBody] DwellDto dto)
        {
            if (dto == null || dto.EventId <= 0 || dto.DwellMs < 0) return Ok(new { ok = true });
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // may be null

            // attach to latest click for this event+user (if any), else create a new row
            var row = await _db.EventClicks
                .Where(c => c.EventId == dto.EventId && c.UserId == userId)
                .OrderByDescending(c => c.ClickedAt)
                .FirstOrDefaultAsync();

            if (row == null)
            {
                row = new EventClick { UserId = userId, EventId = dto.EventId, ClickedAt = DateTime.UtcNow, DwellMs = dto.DwellMs };
                _db.EventClicks.Add(row);
            }
            else
            {
                row.DwellMs = dto.DwellMs;
            }
            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }
        public record DwellDto(int EventId, int DwellMs);
    }
}
