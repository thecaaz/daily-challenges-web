using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services;

namespace DailyChallenges.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FavoritesController : ControllerBase
    {
        private readonly IFavoriteService _favorites;

        public FavoritesController(IFavoriteService favorites)
        {
            _favorites = favorites;
        }

        [HttpPost("{gameId}")]
        public async Task<IActionResult> Add(int gameId)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();

            await _favorites.AddFavoriteAsync(userId.Value, gameId);
            return NoContent();
        }

        [HttpDelete("{gameId}")]
        public async Task<IActionResult> Remove(int gameId)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();

            await _favorites.RemoveFavoriteAsync(userId.Value, gameId);
            return NoContent();
        }
    }
}
