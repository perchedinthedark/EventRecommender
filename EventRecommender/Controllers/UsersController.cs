using EventRecommender.Data;
using EventRecommender.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventRecommender.Controllers
{
    [Authorize]
    public class UsersController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public UsersController(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Users
        public async Task<IActionResult> Index()
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Challenge();

            // everyone except me + mark which ones I'm following
            var users = await _context.Users
                .Where(u => u.Id != me.Id)
                .Select(u => new UserRow
                {
                    Id = u.Id,
                    UserName = u.UserName!,
                    IsFollowed = _context.Friendships.Any(f => f.FollowerId == me.Id && f.FolloweeId == u.Id)
                })
                .OrderBy(u => u.UserName)
                .ToListAsync();

            return View(users);
        }

        // POST: /Users/Follow
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Follow(string followeeId)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Challenge();
            if (string.IsNullOrWhiteSpace(followeeId) || followeeId == me.Id) return BadRequest();

            var exists = await _context.Friendships
                .AnyAsync(f => f.FollowerId == me.Id && f.FolloweeId == followeeId);
            if (!exists)
            {
                _context.Friendships.Add(new Friendship
                {
                    FollowerId = me.Id,
                    FolloweeId = followeeId
                });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: /Users/Unfollow
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unfollow(string followeeId)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Challenge();

            var row = await _context.Friendships
                .FirstOrDefaultAsync(f => f.FollowerId == me.Id && f.FolloweeId == followeeId);
            if (row != null)
            {
                _context.Friendships.Remove(row);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: /Users/Network  (shows my followers & following)
        public async Task<IActionResult> Network()
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Challenge();

            var following = await _context.Friendships
                .Where(f => f.FollowerId == me.Id)
                .Select(f => f.Followee.UserName)
                .OrderBy(n => n)
                .ToListAsync();

            var followers = await _context.Friendships
                .Where(f => f.FolloweeId == me.Id)
                .Select(f => f.Follower.UserName)
                .OrderBy(n => n)
                .ToListAsync();

            return View(new NetworkVm
            {
                Following = following,
                Followers = followers
            });
        }
    }

    public class UserRow
    {
        public string Id { get; set; } = default!;
        public string UserName { get; set; } = default!;
        public bool IsFollowed { get; set; }
    }

    public class NetworkVm
    {
        public List<string> Following { get; set; } = new();
        public List<string> Followers { get; set; } = new();
    }
}
