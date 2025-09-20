using EventRecommender.ML;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EventRecommender.Services;

namespace EventRecommender.Controllers
{
    [Authorize] // keep it behind login for now
    public class AdminController : Controller
    {
        private readonly IRecommenderService _svc;
        private readonly DemoDataSeeder _seed;

        public AdminController(IRecommenderService svc, DemoDataSeeder seed)
        { _svc = svc; _seed = seed; }

        [Authorize]
        public async Task<IActionResult> Seed()
        {
            var (u, e, i) = await _seed.SeedAsync();
            TempData["Msg"] = $"Seeded demo data. Users={u}, Events={e}, Interactions={i}.";
            return RedirectToAction("Index", "Home");
        }

        // GET /Admin/Train
        public async Task<IActionResult> Train()
        {
            await _svc.TrainAsync();
            TempData["Msg"] = "Training done!";
            return RedirectToAction("Index", "Home");
        }

        // GET /Admin/Demo?topN=5
        public async Task<IActionResult> Demo(int topN = 5)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Content("Login first to demo recommendations.");
            var ids = await _svc.RecommendForUserAsync(userId, topN);
            return Content("Recommended EventIds: " + string.Join(", ", ids));
        }
    }
}
