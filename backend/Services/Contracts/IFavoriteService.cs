namespace DailyChallenges.Services;

public interface IFavoriteService
{
    /// <summary>Adding an existing favorite is a no-op. Throws <see cref="Exceptions.NotFoundException"/> for an unknown game.</summary>
    Task AddFavoriteAsync(int userId, int gameId);
    Task RemoveFavoriteAsync(int userId, int gameId);
    Task<List<int>> GetFavoriteGameIdsForUserAsync(int userId);
    Task<bool> ExistsAsync(int userId, int gameId);
}
