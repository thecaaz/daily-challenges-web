using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using DailyChallenges.DTOs;
using DailyChallenges.Services;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(
    IRegistrationService registration,
    ILoginService login,
    ISessionService sessions,
    ICurrentUserService currentUser) : ControllerBase
{
    private const string SessionExpiredMessage = "Session expired. Please log in again.";

    [HttpPost("register")]
    [EnableRateLimiting(AuthRateLimitOptions.PolicyName)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto, CancellationToken ct) =>
        Ok(await registration.RegisterAsync(dto.Username, dto.Password, ct));

    [HttpPost("login")]
    [EnableRateLimiting(AuthRateLimitOptions.PolicyName)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken ct) =>
        Ok(await login.LoginAsync(dto.Username, dto.Password, Response, ct));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await sessions.LogoutAsync(Request, Response, ct);
        return Ok(new { success = true });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var result = await sessions.RefreshAsync(Request, Response, ct);
        if (result == null) return Unauthorized(new { message = SessionExpiredMessage });
        return Ok(result);
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var user = await currentUser.GetCurrentUserAsync(User, ct);
        if (user == null) return Unauthorized();
        return Ok(user);
    }
}
