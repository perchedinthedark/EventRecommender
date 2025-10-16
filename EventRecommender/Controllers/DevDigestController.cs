using System.Globalization;
using System.Net;
using System.Text;
using EventRecommender.Data;
using EventRecommender.ML;
using EventRecommender.Models;
using EventRecommender.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventRecommender.Controllers;

[ApiController]
[Route("api/dev")]
public class DevDigestController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IRecommenderService _recs;
    private readonly IEmailService _email;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DevDigestController> _logger;

    public DevDigestController(
        AppDbContext db,
        IRecommenderService recs,
        IEmailService email,
        UserManager<ApplicationUser> userManager,
        ILogger<DevDigestController> logger)
    {
        _db = db;
        _recs = recs;
        _email = email;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Sends the digest (recs only) to the currently logged-in user.
    /// Optional: ?to=override@domain.com
    /// </summary>
    [HttpPost("digest-me-now")]
    [Authorize]
    public async Task<IActionResult> DigestMeNow([FromQuery] string? to = null, CancellationToken ct = default)
    {
        var me = await _userManager.GetUserAsync(User);
        if (me is null) return Unauthorized();

        var ids = await _recs.RecommendForUserAsync(me.Id, 6);
        var recs = await _db.Events.AsNoTracking()
            .Where(e => ids.Contains(e.EventId))
            .OrderBy(e => e.DateTime)
            .ToListAsync(ct);

        if (recs.Count == 0)
            return BadRequest(new { error = "No recommendations to send." });

        var html = BuildRecsOnlyHtml(me.DisplayName ?? me.Email ?? "there", recs);

        var dest = string.IsNullOrWhiteSpace(to) ? me.Email : to;
        await _email.SendAsync(dest!, "Vaš nedeljni pregled — Eventualno", html);

        return Ok(new { sentTo = dest, recCount = recs.Count, template = "recs-only" });
    }

    // ---------- HTML (recs only; no trending, no CTA) ----------
    private static string BuildRecsOnlyHtml(string recipient, IEnumerable<Event> recommended)
    {
        string H(string? s) => WebUtility.HtmlEncode(s ?? "");
        var culture = new CultureInfo("sr-Latn-RS");
        string Fmt(DateTime dt) => dt.ToString("dddd, d. MMMM yyyy. HH:mm", culture);

        string Card(Event e)
        {
            var imgCell = string.IsNullOrWhiteSpace(e.ImageUrl)
                ? ""
                : $@"
<td style=""width:92px;padding:0;margin:0;border-radius:10px;overflow:hidden;"">
  <img src=""{H(e.ImageUrl)}"" width=""92"" height=""60"" alt="""" style=""display:block;border-radius:10px;object-fit:cover;width:92px;height:60px;"">
</td>
<td style=""width:12px""></td>";

            var organizer = e.Organizer?.Name ?? "—";
            var category = e.Category?.Name ?? "—";
            var location = string.IsNullOrWhiteSpace(e.Location) ? "—" : e.Location;

            var textCell = $@"
<td style=""padding:0;margin:0;"">
  <div style=""font-weight:700;color:#e5edf9;font-size:16px;line-height:1.3;margin:0 0 4px 0;"">{H(e.Title)}</div>
  <div style=""color:#c9d6ea;font-size:13px;line-height:1.5;margin:0;"">{H(Fmt(e.DateTime))}</div>
  <div style=""color:#98a7c3;font-size:13px;line-height:1.5;margin:2px 0 0 0;"">{H(location)} • {H(category)}</div>
  <div style=""color:#98a7c3;font-size:13px;line-height:1.5;margin:2px 0 0 0;"">by {H(organizer)}</div>
</td>";

            var inner = string.IsNullOrWhiteSpace(e.ImageUrl) ? textCell : $"{imgCell}{textCell}";

            return $@"
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""border-collapse:separate;background:#0e2037;border:1px solid #23344d;border-radius:14px;padding:12px;"">
  <tr>{inner}</tr>
</table>";
        }

        string CardsGrid(IEnumerable<Event> list)
        {
            var items = list.Take(6).ToList();
            var sb = new StringBuilder();
            for (int i = 0; i < items.Count; i += 2)
            {
                var left = Card(items[i]);
                var right = (i + 1 < items.Count) ? Card(items[i + 1]) : "";
                sb.Append($@"
<tr>
  <td style=""vertical-align:top;width:50%;padding:6px;"">{left}</td>
  <td style=""vertical-align:top;width:50%;padding:6px;"">{right}</td>
</tr>");
            }

            return $@"
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""border-collapse:collapse;margin:4px -6px 6px -6px;"">
  {sb}
</table>";
        }

        return $@"<!doctype html>
<html lang=""sr"">
  <body style=""margin:0;padding:0;background:#061325;"">
    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""border-collapse:collapse;background:#061325;padding:24px 0;"">
      <tr>
        <td align=""center"">
          <table role=""presentation"" width=""680"" cellpadding=""0"" cellspacing=""0"" style=""width:100%;max-width:680px;border-collapse:separate;background:#0b1a33;border-radius:18px;box-shadow:0 12px 28px rgba(0,0,0,.35);overflow:hidden;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Inter,Arial,sans-serif;"">
            <tr>
              <td style=""padding:18px 20px;border-bottom:1px solid #192a46;background:linear-gradient(135deg,#0b1a33,#0c213f);"">
                <div style=""color:#eaf1ff;font-size:22px;font-weight:800;letter-spacing:.2px;"">Hi {H(recipient)}, here are your weekly picks ✨</div>
              </td>
            </tr>

            <tr>
              <td style=""padding:16px 20px 6px 20px;color:#eaf1ff;font-size:20px;font-weight:700;"">Recommended for you</td>
            </tr>

            <tr>
              <td style=""padding:6px 10px 18px 10px;"">
                {CardsGrid(recommended)}
              </td>
            </tr>

            <tr>
              <td style=""background:#08152b;color:#8aa0c7;font-size:12px;padding:14px 20px;border-top:1px solid #192a46;"">
                Sent automatically from Eventualno. Please don’t reply to this email.
              </td>
            </tr>
          </table>
        </td>
      </tr>
    </table>
  </body>
</html>";
    }
}
