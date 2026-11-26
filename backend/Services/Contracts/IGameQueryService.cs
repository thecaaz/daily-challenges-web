using System.Security.Claims;
using DailyChallenges.DTOs;

namespace DailyChallenges.Services;

public interface IGameQueryService
{
    /// <summary>Every game; for a signed-in user with HasSubmittedForLatest and IsFavorite set.</summary>
    Task<List<GameDto>> GetAllAsync(ClaimsPrincipal? user = null);

    /// <summary>The game's image, or (null, null) when it has none.</summary>
    Task<(byte[]? Data, string? ContentType)> GetImageAsync(int id);

    /// <summary>
    /// The game plus the parts named in the comma-separated <paramref name="include"/> ("availableDates",
    /// "hasSubmitted"; all when empty). A failing part is reported in Errors and falls back to its empty value.
    /// Throws NotFoundException for a missing game.
    /// </summary>
    Task<GameOverviewDto> GetOverviewAsync(int gameId, ClaimsPrincipal? user, string? include = null);
}
