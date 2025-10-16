using System.Globalization;
using System.Net;
using System.Text;
using EventRecommender.Data;
using EventRecommender.ML;
using EventRecommender.Models;
using EventRecommender.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventRecommender.Services
{
    public sealed class WeeklyDigestService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<WeeklyDigestService> _logger;
        private readonly IConfiguration _cfg;
        private readonly IHostEnvironment _env;

        public WeeklyDigestService(
            IServiceScopeFactory scopeFactory,
            ILogger<WeeklyDigestService> logger,
            IConfiguration cfg,
            IHostEnvironment env)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _cfg = cfg;
            _env = env;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var enabled = _cfg.GetValue<bool>("WeeklyDigest:Enabled", true);
            var runInDev = _cfg.GetValue<bool>("WeeklyDigest:RunInDevelopment", false);
            var runNow = _cfg.GetValue<bool>("WeeklyDigest:RunOnStartup", false);

            if (!enabled) { _logger.LogInformation("WeeklyDigest disabled."); return; }
            if (_env.IsDevelopment() && !runInDev)
            { _logger.LogInformation("WeeklyDigest disabled in Development."); return; }

            var day = ParseDay(_cfg["WeeklyDigest:DayOfWeek"]) ?? DayOfWeek.Monday;
            var hour = _cfg.GetValue<int?>("WeeklyDigest:UtcHour") ?? 9;

            if (runNow && !stoppingToken.IsCancellationRequested)
                await SendAllAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTimeOffset.UtcNow;
                var next = NextWeekly(now, day, hour);
                var delay = next - now;
                _logger.LogInformation("Next weekly digest at {Next} (in {Delay}).", next, delay);
                try { await Task.Delay(delay, stoppingToken); } catch { break; }
                if (!stoppingToken.IsCancellationRequested) await SendAllAsync(stoppingToken);
            }
        }

        private async Task SendAllAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var recs = scope.ServiceProvider.GetRequiredService<IRecommenderService>();
            var mail = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var users = await db.Users.AsNoTracking().ToListAsync(ct);
            _logger.LogInformation("Sending weekly digests to {Count} users…", users.Count);

            foreach (var user in users)
            {
                if (ct.IsCancellationRequested) break;
                if (string.IsNullOrWhiteSpace(user.Email)) continue;

                int[] ids;
                try { ids = await recs.RecommendForUserAsync(user.Id, 6); }
                catch (Exception ex) { _logger.LogWarning(ex, "Recs failed for {User}", user.Id); continue; }

                if (ids.Length == 0) continue;

                var recommended = await db.Events.AsNoTracking()
                    .Where(e => ids.Contains(e.EventId))
                    .OrderBy(e => e.DateTime)
                    .ToListAsync(ct);

                if (recommended.Count == 0) continue;

                var html = BuildRecsOnlyHtml(user.DisplayName ?? user.Email!, recommended);

                try
                {
                    await mail.SendAsync(user.Email!, "Vaš nedeljni pregled — Eventualno", html);
                    _logger.LogInformation("Sent weekly digest to {Email}", user.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed sending digest to {Email}", user.Email);
                }
            }

            _logger.LogInformation("Weekly digest run finished.");
        }

        // ------------ helpers (schedule + HTML) ------------
        private static DayOfWeek? ParseDay(string? s)
            => Enum.TryParse<DayOfWeek>(s, true, out var d) ? d : null;

        private static DateTimeOffset NextWeekly(DateTimeOffset nowUtc, DayOfWeek day, int hourUtc)
        {
            var targetToday = new DateTimeOffset(nowUtc.Year, nowUtc.Month, nowUtc.Day, hourUtc, 0, 0, TimeSpan.Zero);
            var daysUntil = ((int)day - (int)nowUtc.DayOfWeek + 7) % 7;
            if (daysUntil == 0 && nowUtc >= targetToday) daysUntil = 7;
            return targetToday.AddDays(daysUntil);
        }

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
}
