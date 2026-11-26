using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DailyChallenges.DTOs;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Controllers;

[ApiController]
[Route("api")]
public sealed class AchievementsController(IAchievementService achievements) : ControllerBase
{
    /// <summary>Returns the full achievement catalog with the user's unlock state merged in.</summary>
    [HttpGet("users/{userId:int}/achievements")]
    public async Task<ActionResult<List<AchievementDto>>> GetForUser(int userId) =>
        Ok(await achievements.GetForUserAsync(userId));

    /// <summary>Returns the full achievement catalog (no unlock state). Useful for listing all achievements.</summary>
    [HttpGet("achievements")]
    public ActionResult<IReadOnlyList<AchievementCatalogEntryDto>> GetCatalog() =>
        Ok(achievements.GetCatalog());

    /// <summary>Returns the authenticated user's achievements (convenience endpoint).</summary>
    [HttpGet("users/me/achievements")]
    [Authorize]
    public async Task<ActionResult<List<AchievementDto>>> GetMine([CurrentUserId(WhenMissing = MissingUserIdResponse.Unauthorized)] int userId) =>
        Ok(await achievements.GetForUserAsync(userId));
}
