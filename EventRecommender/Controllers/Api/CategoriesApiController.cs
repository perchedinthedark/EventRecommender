using EventRecommender.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventRecommender.Controllers.Api
{
    [ApiController]
    [Route("api/categories")]
    public class CategoriesApiController : ControllerBase
    {
        private readonly AppDbContext _db;
        public CategoriesApiController(AppDbContext db) { _db = db; }

        // GET /api/categories
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var cats = await _db.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new { id = c.CategoryId, name = c.Name })
                .ToListAsync();
            return Ok(cats);
        }
    }
}
