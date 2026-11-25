using DailyChallenges.Models;
using DailyChallenges.Repositories;

namespace DailyChallenges.Services;

public sealed class FavoriteService(IFavoriteRepository favorites, IGameRepository games) : IFavoriteService
{
    public async Task AddFavoriteAsync(int userId, int gameId)
    {
        _ = await games.GetByIdAsync(gameId) ?? throw new KeyNotFoundException("Game not found");
        await favorites.AddAsync(new Favorite { UserId = userId, GameId = gameId });
    }

    public Task RemoveFavoriteAsync(int userId, int gameId) => favorites.RemoveAsync(userId, gameId);

    public Task<List<int>> GetFavoriteGameIdsForUserAsync(int userId) => favorites.GetFavoriteGameIdsForUserAsync(userId);

    public Task<bool> ExistsAsync(int userId, int gameId) => favorites.ExistsAsync(userId, gameId);
}
