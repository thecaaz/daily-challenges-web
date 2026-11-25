using DailyChallenges.DTOs;
using DailyChallenges.Services;
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
    public Task<ActionResult<UserProfileDto>> GetProfile(int id, int topGames = DefaultTopGames) =>
        GetProfileResultAsync(id, topGames);

    [HttpGet("me/profile")]
    [Authorize]
    public async Task<ActionResult<UserProfileDto>> GetMyProfile(int topGames = DefaultTopGames)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        return await GetProfileResultAsync(userId.Value, topGames);
    }

    private async Task<ActionResult<UserProfileDto>> GetProfileResultAsync(int userId, int topGames)
    {
        var profile = await profiles.GetProfileAsync(userId, Math.Clamp(topGames, MinTopGames, MaxTopGames), User);
        if (profile == null) return NotFound();
        return Ok(profile);
    }
}
