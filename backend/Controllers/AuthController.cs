using Microsoft.AspNetCore.Mvc;
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
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            try
            {
                var u = await _auth.RegisterAsync(dto.Username, dto.Password);
                return Ok(u);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            try
            {
                var u = await _auth.LoginAsync(dto.Username, dto.Password, Response);
                return Ok(u);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "Invalid credentials" });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _auth.LogoutAsync(Response);
            return Ok(new { success = true });
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
