using EventRecommender.Data;
using EventRecommender.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EventRecommender.Controllers.Api
{
    [ApiController]
    [Route("api/social")]
    public class SocialApiController : ControllerBase
    {
        private readonly AppDbContext _db;
        public SocialApiController(AppDbContext db) { _db = db; }

        string? UID() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        [Authorize]
        [HttpPost("follow")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Follow([FromBody] FollowDto dto)
        {
            var uid = UID(); if (uid == null) return Unauthorized();
            if (string.IsNullOrWhiteSpace(dto.FolloweeId) || dto.FolloweeId == uid) return BadRequest();

            var exists = await _db.Friendships.AnyAsync(f => f.FollowerId == uid && f.FolloweeId == dto.FolloweeId);
            if (!exists)
            {
                _db.Friendships.Add(new Friendship { FollowerId = uid, FolloweeId = dto.FolloweeId });
                await _db.SaveChangesAsync();
            }
            return Ok(new { ok = true });
        }
        public record FollowDto(string FolloweeId);

        [Authorize]
        [HttpPost("unfollow")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Unfollow([FromBody] FollowDto dto)
        {
            var uid = UID(); if (uid == null) return Unauthorized();
            var row = await _db.Friendships.FirstOrDefaultAsync(f => f.FollowerId == uid && f.FolloweeId == dto.FolloweeId);
            if (row != null) { _db.Friendships.Remove(row); await _db.SaveChangesAsync(); }
            return Ok(new { ok = true });
        }

        [Authorize]
        [HttpGet("following")]
        public async Task<IActionResult> Following()
        {
            var uid = UID(); if (uid == null) return Unauthorized();

            var ids = await _db.Friendships
                .Where(f => f.FollowerId == uid)
                .Select(f => f.FolloweeId)
                .ToListAsync();

            var users = await _db.Users
                .Where(u => ids.Contains(u.Id))
                .Select(u => new { id = u.Id, userName = u.UserName, displayName = u.DisplayName, email = u.Email })
                .ToListAsync();

            return Ok(users);
        }

        // NEW: Followers list (people who follow ME)
        [Authorize]
        [HttpGet("followers")]
        public async Task<IActionResult> Followers()
        {
            var uid = UID(); if (uid == null) return Unauthorized();

            var ids = await _db.Friendships
                .Where(f => f.FolloweeId == uid)
                .Select(f => f.FollowerId)
                .ToListAsync();

            var users = await _db.Users
                .Where(u => ids.Contains(u.Id))
                .Select(u => new { id = u.Id, userName = u.UserName, displayName = u.DisplayName, email = u.Email })
                .ToListAsync();

            return Ok(users);
        }

        // friends-going count for a given event
        [Authorize]
        [HttpGet("friends-going")]
        public async Task<IActionResult> FriendsGoing([FromQuery] int eventId)
        {
            var uid = UID(); if (uid == null) return Unauthorized();

            var followees = await _db.Friendships
                .Where(f => f.FollowerId == uid)
                .Select(f => f.FolloweeId)
                .ToListAsync();

            var count = await _db.UserEventInteractions
                .Where(i => i.EventId == eventId && i.Status == InteractionStatus.Going && followees.Contains(i.UserId))
                .CountAsync();

            return Ok(new { count });
        }
    }
}
