using System.Diagnostics;
using EventRecommender.Data;
using EventRecommender.ML;
using EventRecommender.Models;
using EventRecommender.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventRecommender.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IRecommenderService _svc;
        private readonly ITrendingService _trending;
        private readonly UserManager<ApplicationUser> _um;
        private readonly AppDbContext _db;

        public HomeController(
            ILogger<HomeController> logger,
            IRecommenderService svc,
            ITrendingService trending,
            UserManager<ApplicationUser> um,
            AppDbContext db)
        {
            _logger = logger; _svc = svc; _trending = trending; _um = um; _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var vm = new HomeVm();
            string? uid = null;

            if (User?.Identity?.IsAuthenticated == true)
                uid = _um.GetUserId(User);

            // Personalized (if models exist and user is not ultra-cold)
            if (uid != null && _svc.ModelsExist())
            {
                var ids = await _svc.RecommendForUserAsync(uid, 6);
                var recEvents = await _db.Events.AsNoTracking()
                    .Where(e => ids.Contains(e.EventId))
                    .Include(e => e.Category)
                    .Include(e => e.Venue)
                    .Include(e => e.Organizer)
                    .ToListAsync();

                // (optional) keep model order:
                vm.Recommended = recEvents
                    .OrderBy(e => Array.IndexOf(ids, e.EventId))
                    .ToList();
            }

            // Trending overall + category-aware (great for cold-start)
            var (overallIds, byCatIds) = await _trending.GetTrendingForUserAsync(uid, perList: 6, categoriesToShow: 2);

            // --- Overall: materialize first, then order in memory ---
            var overallEvents = await _db.Events.AsNoTracking()
                .Where(e => overallIds.Contains(e.EventId))
                .Include(e => e.Category)
                .Include(e => e.Venue)
                .Include(e => e.Organizer)
                .ToListAsync();

            vm.TrendingOverall = overallEvents
                .OrderBy(e => overallIds.IndexOf(e.EventId))
                .ToList();

            // --- By category: same trick ---
            var cats = await _db.Categories.AsNoTracking()
                .ToDictionaryAsync(c => c.CategoryId, c => c.Name);

            foreach (var kv in byCatIds)
            {
                var catIds = kv.Value;
                var catEvents = await _db.Events.AsNoTracking()
                    .Where(e => catIds.Contains(e.EventId))
                    .Include(e => e.Category)
                    .Include(e => e.Venue)
                    .Include(e => e.Organizer)
                    .ToListAsync();

                var catOrdered = catEvents
                    .OrderBy(e => catIds.IndexOf(e.EventId))
                    .ToList();

                vm.TrendingByCategory.Add(new HomeVm.CategorySection
                {
                    CategoryId = kv.Key,
                    CategoryName = cats.TryGetValue(kv.Key, out var name) ? name : $"Category {kv.Key}",
                    Events = catOrdered
                });
            }

            ViewBag.Msg = TempData["Msg"];
            return View(vm);
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    public class HomeVm
    {
        public List<Event> Recommended { get; set; } = new();

        public List<Event> TrendingOverall { get; set; } = new();

        public List<CategorySection> TrendingByCategory { get; set; } = new();
        public class CategorySection
        {
            public int CategoryId { get; set; }
            public string CategoryName { get; set; } = "";
            public List<Event> Events { get; set; } = new();
        }
    }
}
