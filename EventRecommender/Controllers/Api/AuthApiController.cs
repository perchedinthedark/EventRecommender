using EventRecommender.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EventRecommender.Controllers.Api
{
    [ApiController]
    [Route("api/auth")]
    public class AuthApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _um;
        private readonly SignInManager<ApplicationUser> _sm;

        public AuthApiController(UserManager<ApplicationUser> um, SignInManager<ApplicationUser> sm)
        { _um = um; _sm = sm; }

        [HttpPost("register")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest("email/password required");

            var user = new ApplicationUser { UserName = dto.Email, Email = dto.Email };
            var res = await _um.CreateAsync(user, dto.Password);
            if (!res.Succeeded)
                return BadRequest(string.Join(" | ", res.Errors.Select(e => e.Description)));

            await _sm.SignInAsync(user, isPersistent: true);

            return Ok(new { id = user.Id, email = user.Email, userName = user.UserName });
        }
        public record RegisterDto(string Email, string Password);

        [HttpPost("login")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest("email/password required");

            var user = await _um.FindByEmailAsync(dto.Email);
            if (user == null) return Unauthorized();

            var res = await _sm.PasswordSignInAsync(user, dto.Password, isPersistent: true, lockoutOnFailure: false);
            if (!res.Succeeded) return Unauthorized();

            return Ok(new { id = user.Id, email = user.Email, userName = user.UserName });
        }
        public record LoginDto(string Email, string Password);

        [Authorize]
        [HttpPost("logout")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _sm.SignOutAsync();
            return Ok(new { ok = true });
        }

        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            if (!(User?.Identity?.IsAuthenticated ?? false)) return Unauthorized();

            var user = await _um.GetUserAsync(User);
            if (user == null) return Unauthorized();

            return Ok(new { id = user.Id, email = user.Email, userName = user.UserName });
        }
    }
}
