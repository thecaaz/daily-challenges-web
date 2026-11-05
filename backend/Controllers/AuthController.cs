using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using DailyChallenges.DTOs;
using DailyChallenges.Services;

namespace DailyChallenges.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;

        public AuthController(IAuthService auth)
        {
            _auth = auth;
        }

        [HttpPost("register")]
        [EnableRateLimiting(AuthRateLimitOptions.PolicyName)]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var u = await _auth.RegisterAsync(dto.Username, dto.Password);
            return Ok(u);
        }

        [HttpPost("login")]
        [EnableRateLimiting(AuthRateLimitOptions.PolicyName)]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var result = await _auth.LoginAsync(dto.Username, dto.Password, Response);
            return Ok(result);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _auth.LogoutAsync(Request, Response);
            return Ok(new { success = true });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            var result = await _auth.RefreshAsync(Request, Response);
            if (result == null) return Unauthorized(new { message = "Session expired. Please log in again." });
            return Ok(result);
        }

        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var u = await _auth.GetCurrentUserAsync(User);
            if (u == null) return Unauthorized();
            return Ok(u);
        }

        // JWT generation moved to AuthService
    }
}
