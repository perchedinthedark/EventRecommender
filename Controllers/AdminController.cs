using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EventRecommender.ML;

namespace EventRecommender.Controllers
{
    [Authorize] // keep it behind login for now
    public class AdminController : Controller
    {
        private readonly IRecommenderService _svc;
        public AdminController(IRecommenderService svc) => _svc = svc;

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
            var uid = User?.Identity?.IsAuthenticated == true ? User.Identity!.Name! : null;
            if (uid == null) return Content("Login first to demo recommendations.");
            var ids = await _svc.RecommendForUserAsync(uid, topN);
            return Content("Recommended EventIds: " + string.Join(", ", ids));
        }
    }
}
