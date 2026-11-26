using DailyChallenges.DTOs;
using DailyChallenges.Services.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyChallenges.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(IUserProfileService profiles) : ControllerBase
{
    private const int DefaultTopGames = 10;
    private const int MinTopGames = 1;
    private const int MaxTopGames = 50;

    [HttpGet("{id}/profile")]
    public Task<ActionResult<UserProfileDto>> GetProfile(int id, int topGames = DefaultTopGames, CancellationToken ct = default) =>
        GetProfileResultAsync(id, topGames, ct);

    [HttpGet("me/profile")]
    [Authorize]
    public Task<ActionResult<UserProfileDto>> GetMyProfile([CurrentUserId(WhenMissing = MissingUserIdResponse.Unauthorized)] int userId, int topGames = DefaultTopGames, CancellationToken ct = default) =>
        GetProfileResultAsync(userId, topGames, ct);

    private async Task<ActionResult<UserProfileDto>> GetProfileResultAsync(int userId, int topGames, CancellationToken ct)
    {
        var profile = await profiles.GetProfileAsync(userId, Math.Clamp(topGames, MinTopGames, MaxTopGames), User, ct);
        if (profile == null) return NotFound();
        return Ok(profile);
    }
}
