// File: Services/WeeklyDigestService.cs
using System.Globalization;
using System.Text;
using EventRecommender.Data;
using EventRecommender.Models;
using EventRecommender.Services.Email;
using EventRecommender.ML;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace EventRecommender.Services
{
    /// <summary>
    /// Runs on a weekly schedule (default: Monday 09:00 UTC)
    /// Disabled in Development unless WeeklyDigest:RunInDevelopment=true.
    /// Uses scoped dependencies per run (DB, recommender, email).
    /// </summary>
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

            if (!enabled)
            {
                _logger.LogInformation("WeeklyDigestService disabled via config (WeeklyDigest:Enabled=false).");
                return;
            }

            if (_env.IsDevelopment() && !runInDev)
            {
                _logger.LogInformation("WeeklyDigestService is disabled in Development (set WeeklyDigest:RunInDevelopment=true to enable).");
                return;
            }

            // Schedule settings (UTC)
            var dayOfWeek = ParseDayOfWeek(_cfg["WeeklyDigest:DayOfWeek"]) ?? DayOfWeek.Monday;
            var hourUtc = _cfg.GetValue<int?>("WeeklyDigest:UtcHour") ?? 9;  // 09:00 UTC by default

            var runOnStartup = _cfg.GetValue<bool>("WeeklyDigest:RunOnStartup", false);

            _logger.LogInformation("WeeklyDigestService running. Schedule: {Day} {HourUtc:D2}:00 UTC. RunOnStartup={RunOnStartup}",
                dayOfWeek, hourUtc, runOnStartup);

            // Optional: immediate run once
            if (runOnStartup && !stoppingToken.IsCancellationRequested)
            {
                await SendAllDigestsAsync(stoppingToken);
            }

            // Main weekly loop
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTimeOffset.UtcNow;
                var next = NextWeeklyOccurrence(now, dayOfWeek, hourUtc);

                var delay = next - now;
                if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

                _logger.LogInformation("Next weekly digest scheduled at {NextUtc} (in {Delay}).", next, delay);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    await SendAllDigestsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Weekly digest run failed.");
                }
            }
        }

        private static DayOfWeek? ParseDayOfWeek(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (Enum.TryParse<DayOfWeek>(s, ignoreCase: true, out var d)) return d;

            // also accept localized names, just in case
            for (int i = 0; i < 7; i++)
            {
                var name = CultureInfo.InvariantCulture.DateTimeFormat.GetDayName((DayOfWeek)i);
                if (string.Equals(name, s, StringComparison.OrdinalIgnoreCase))
                    return (DayOfWeek)i;
            }
            return null;
        }

        private static DateTimeOffset NextWeeklyOccurrence(DateTimeOffset nowUtc, DayOfWeek day, int hourUtc)
        {
            // next day-of-week at the specified hour (UTC)
            var target = new DateTimeOffset(nowUtc.Year, nowUtc.Month, nowUtc.Day, hourUtc, 0, 0, TimeSpan.Zero);
            int daysUntil = ((int)day - (int)nowUtc.DayOfWeek + 7) % 7;
            if (daysUntil == 0 && nowUtc >= target)
            {
                daysUntil = 7;
            }
            var next = target.AddDays(daysUntil);
            return next;
        }

        private async Task SendAllDigestsAsync(CancellationToken ct)
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
                try
                {
                    ids = await recs.RecommendForUserAsync(user.Id, topN: 6);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Recommender failed for user {UserId}; skipping.", user.Id);
                    continue;
                }

                if (ids == null || ids.Length == 0) continue;

                var events_ = await db.Events
                    .AsNoTracking()
                    .Where(e => ids.Contains(e.EventId))
                    .OrderBy(e => e.DateTime)
                    .ToListAsync(ct);

                if (events_.Count == 0) continue;

                // Simple body (HTML-friendly). If your EmailService uses plain text,
                // you can strip tags or adjust EmailService to IsBodyHtml=true.
                var sb = new StringBuilder();
                sb.Append("<h2>Vaš nedeljni pregled događaja</h2><ul>");
                foreach (var e in events_)
                {
                    sb.Append($"<li><strong>{e.Title}</strong> – {e.DateTime:g}");
                    if (!string.IsNullOrWhiteSpace(e.Location))
                        sb.Append($" @ {e.Location}");
                    sb.Append("</li>");
                }
                sb.Append("</ul>");

                try
                {
                    await mail.SendAsync(
                        to: user.Email!,
                        subject: "Vaš nedeljni pregled događaja",
                        body: sb.ToString()
                    );
                    _logger.LogInformation("Sent weekly digest to {Email}.", user.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed sending digest to {Email}.", user.Email);
                }
            }

            _logger.LogInformation("Weekly digest run finished.");
        }
    }
}

