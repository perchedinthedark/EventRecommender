using EventRecommender.Data;
using EventRecommender.ML;
using EventRecommender.Models;
using EventRecommender.Services;
using EventRecommender.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace EventRecommender.Controllers;

[ApiController]
[Route("api/dev")]
public class DevDigestController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IRecommenderService _recs;
    private readonly ITrendingService _trending;
    private readonly IEmailService _email;

    public DevDigestController(AppDbContext db, IRecommenderService recs, ITrendingService trending, IEmailService email)
    {
        _db = db;
        _recs = recs;
        _trending = trending;
        _email = email;
    }

    // Hit: POST /api/dev/digest-me-now
    // Sends a digest to the currently logged-in user only (safer for testing)
    [HttpPost("digest-me-now")]
    [Authorize]
    public async Task<IActionResult> DigestMeNow([FromQuery] int topN = 6)
    {
        var uid = User?.Identity?.Name;
        if (string.IsNullOrEmpty(uid))
            return Unauthorized("Not signed in.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == uid);
        if (user == null || string.IsNullOrWhiteSpace(user.Email))
            return BadRequest("User not found or has no email.");

        var recIds = await _recs.RecommendForUserAsync(user.Id, topN);
        var events = await _db.Events
            .AsNoTracking()
            .Where(e => recIds.Contains(e.EventId))
            .Select(e => new { e.Title, e.Description, e.DateTime, e.Location })
            .ToListAsync();

        var (overall, _) = await _trending.GetTrendingForUserAsync(user.Id, 6, 0);
        var trending = await _db.Events.AsNoTracking()
            .Where(e => overall.Contains(e.EventId))
            .Select(e => new { e.Title, e.DateTime, e.Location })
            .ToListAsync();

        var html = new StringBuilder()
            .Append("<h2>Vaš nedeljni pregled — Event Recommender</h2>")
            .Append("<p>Preporučeni događaji za vas:</p><ul>");

        foreach (var e in events)
            html.Append($"<li><strong>{e.Title}</strong> — {e.DateTime:g} — {e.Location}</li>");

        html.Append("</ul><hr/><p>Trending:</p><ul>");
        foreach (var t in trending)
            html.Append($"<li>{t.Title} — {t.DateTime:g} — {t.Location}</li>");
        html.Append("</ul>");

        await _email.SendAsync(user.Email!, "Vaš nedeljni pregled događaja", html.ToString());
        return Ok(new { sentTo = user.Email, recCount = events.Count, trendingCount = trending.Count });
    }
}
