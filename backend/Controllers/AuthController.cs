using Microsoft.AspNetCore.Mvc;
using DailyChallenges.Data;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DailyChallenges.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly DailyChallenges.Services.IAuthService _auth;

        public AuthController(DailyChallenges.Services.IAuthService auth)
        {
            _auth = auth;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            try
            {
                var u = await _auth.RegisterAsync(dto.Username, dto.Password);
                return Ok(new { id = u.Id, username = u.Username, isAdmin = u.IsAdmin });
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
                return Ok(new { id = u.Id, username = u.Username, isAdmin = u.IsAdmin });
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
            return Ok(new { id = u.Id, username = u.Username, isAdmin = u.IsAdmin });
        }

        // JWT generation moved to AuthService
    }

    public record RegisterDto(string Username, string Password);
    public record LoginDto(string Username, string Password);
}
