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
    public async Task<IActionResult> Add([CurrentUserId] int userId, int gameId, CancellationToken ct)
    {
        await favorites.AddFavoriteAsync(userId, gameId, ct);
        return NoContent();
    }

    [HttpDelete("{gameId}")]
    public async Task<IActionResult> Remove([CurrentUserId] int userId, int gameId, CancellationToken ct)
    {
        await favorites.RemoveFavoriteAsync(userId, gameId, ct);
        return NoContent();
    }
}
