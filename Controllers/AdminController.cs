using EventRecommender.ML;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EventRecommender.Services;
using EventRecommender.Data;
using Microsoft.EntityFrameworkCore;
using EventRecommender.Models;
using EventRecommender.ViewModels;

namespace EventRecommender.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly IRecommenderService _svc;
        private readonly DemoDataSeeder _seed;
        private readonly AppDbContext _db;
        private readonly RecommenderConfig _cfg;

        public AdminController(IRecommenderService svc, DemoDataSeeder seed, AppDbContext db, RecommenderConfig cfg)
        {
            _svc = svc; _seed = seed; _db = db; _cfg = cfg;
        }

        [Authorize]
        public async Task<IActionResult> Seed()
        {
            var (u, e, i) = await _seed.SeedAsync();
            TempData["Msg"] = $"Seeded demo data. Users={u}, Events={e}, Interactions={i}.";
            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> Train()
        {
            try
            {
                await _svc.TrainAsync();
                TempData["Msg"] = "Training done!";
            }
            catch (Exception ex)
            {
                TempData["Msg"] = "Training failed: " + ex.Message;
            }
            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> Demo(int topN = 5)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Content("Login first to demo recommendations.");
            var ids = await _svc.RecommendForUserAsync(userId, topN);
            return Content("Recommended EventIds: " + string.Join(", ", ids));
        }

        // GET: /Admin/Metrics
        public async Task<IActionResult> Metrics()
        {
            var now = DateTime.UtcNow;

            var users = await _db.Users.CountAsync();
            var eventsCount = await _db.Events.CountAsync();
            var interactions = await _db.UserEventInteractions.CountAsync();
            var clicks = await _db.EventClicks.CountAsync();

            DateTime? lastTrain = null;
            if (_svc.ModelsExist())
            {
                var t1 = System.IO.File.Exists(_cfg.MfModelPath) ? System.IO.File.GetLastWriteTimeUtc(_cfg.MfModelPath) : (DateTime?)null;
                var t2 = System.IO.File.Exists(_cfg.RankModelPath) ? System.IO.File.GetLastWriteTimeUtc(_cfg.RankModelPath) : (DateTime?)null;
                lastTrain = new[] { t1, t2 }.Where(t => t.HasValue).Select(t => t!.Value).DefaultIfEmpty().Max();
            }

            var lastServe = await _db.RecommendationLogs
                .OrderByDescending(r => r.RecommendedAt)
                .Select(r => (DateTime?)r.RecommendedAt)
                .FirstOrDefaultAsync();

            var coldStartUsers = await _db.Users
                .CountAsync(u =>
                    !_db.UserEventInteractions.Any(i => i.UserId == u.Id) &&
                    !_db.EventClicks.Any(c => c.UserId == u.Id));

            var candidatePool = await _db.Events.CountAsync(e => e.DateTime >= now);

            var recentClicks = await (
                from c in _db.EventClicks.AsNoTracking()
                join e in _db.Events.AsNoTracking() on c.EventId equals e.EventId
                join u in _db.Users.AsNoTracking() on c.UserId equals u.Id into uj
                from u in uj.DefaultIfEmpty()
                orderby c.ClickedAt descending
                select new AdminMetricsVm.ClickRow
                {
                    WhenUtc = c.ClickedAt,
                    EventTitle = e.Title,
                    UserName = u != null ? u.UserName : "(anon)",
                    DwellMs = c.DwellMs
                }
            ).Take(20).ToListAsync();

            var vm = new AdminMetricsVm
            {
                Users = users,
                Events = eventsCount,
                Interactions = interactions,
                Clicks = clicks,
                ModelsExist = _svc.ModelsExist(),
                LastTrainUtc = lastTrain,
                LastServeUtc = lastServe,
                ColdStartUsers = coldStartUsers,
                CandidatePoolUpcoming = candidatePool,
                RecentClicks = recentClicks
            };

            return View(vm);
        }
    }
}


