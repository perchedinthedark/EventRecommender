using EventRecommender.Data;
using EventRecommender.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EventRecommender.Controllers.Api
{
    [ApiController]
    [Route("api/users")]
    public class UsersApiController : ControllerBase
    {
        private readonly AppDbContext _db;
        public UsersApiController(AppDbContext db) { _db = db; }

        private string? UID() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        // GET /api/users/search?q=alice&limit=10
        [Authorize]
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 12)
        {
            var uid = UID();
            if (uid is null) return Unauthorized();

            q = (q ?? string.Empty).Trim();
            if (q.Length == 0) return Ok(Array.Empty<object>());

            limit = Math.Clamp(limit, 1, 25);

            // Case-insensitive contains using LIKE; search username OR email
            var results = await _db.Users
                .Where(u =>
                    u.Id != uid &&
                    (EF.Functions.Like(u.UserName!, $"%{q}%") ||
                     EF.Functions.Like(u.Email!, $"%{q}%")))
                .OrderBy(u => u.UserName)
                .ThenBy(u => u.Email)
                .Take(limit)
                .Select(u => new
                {
                    id = u.Id,
                    userName = u.UserName,
                    email = u.Email
                })
                .ToListAsync();

            return Ok(results);
        }
    }
}
