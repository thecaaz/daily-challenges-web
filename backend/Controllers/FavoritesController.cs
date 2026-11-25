using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DailyChallenges.Services;

namespace DailyChallenges.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class FavoritesController(IFavoriteService favorites) : ControllerBase
{
    [HttpPost("{gameId}")]
    public async Task<IActionResult> Add([CurrentUserId] int userId, int gameId)
    {
        await favorites.AddFavoriteAsync(userId, gameId);
        return NoContent();
    }

    [HttpDelete("{gameId}")]
    public async Task<IActionResult> Remove([CurrentUserId] int userId, int gameId)
    {
        await favorites.RemoveFavoriteAsync(userId, gameId);
        return NoContent();
    }
}
