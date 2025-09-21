using EventRecommender.Data;
using EventRecommender.ML;
using EventRecommender.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NuGet.Packaging.Signing;
using System.Diagnostics;

namespace EventRecommender.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IRecommenderService _svc;
        private readonly UserManager<ApplicationUser> _um;
        private readonly AppDbContext _db;

        public HomeController(ILogger<HomeController> logger, IRecommenderService svc, UserManager<ApplicationUser> um, AppDbContext db)
               { _logger = logger; _svc = svc; _um = um; _db = db; }

        public async Task<IActionResult> Index()
        {
            var vm = new HomeVm();

            // Use the Identity GUID if logged in; empty string for anon (service will cold-start fallback)
            var uid = _um.GetUserId(User) ?? "";

            // Always call the recommender; it handles models-missing and cold-start internally
            var ids = await _svc.RecommendForUserAsync(uid, topN: 6);

            if (ids.Length > 0)
            {
                // Fetch the events and preserve the recommendation order
                var order = ids.Select((id, idx) => new { id, idx })
                               .ToDictionary(x => x.id, x => x.idx);

                vm.Recommended = await _db.Events
                    .AsNoTracking()
                    .Where(e => ids.Contains(e.EventId))
                    .Include(e => e.Category)
                    .Include(e => e.Venue)
                    .Include(e => e.Organizer)
                    .ToListAsync();

                vm.Recommended = vm.Recommended.OrderBy(e => order[e.EventId]).ToList();
            }

            ViewBag.Msg = TempData["Msg"];
            return View(vm);
        }


        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    public class HomeVm
    {
        public List<Event> Recommended { get; set; } = new();
    }
}
