using DailyChallenges.Exceptions;
using DailyChallenges.Models;
using DailyChallenges.Repositories;

namespace DailyChallenges.Services;

public sealed class FavoriteService(IFavoriteRepository favorites, IGameRepository games, TimeProvider time) : IFavoriteService
{
    public async Task AddFavoriteAsync(int userId, int gameId)
    {
        _ = await games.GetByIdAsync(gameId) ?? throw new NotFoundException("Game not found");
        await favorites.AddAsync(new Favorite { UserId = userId, GameId = gameId, CreatedAt = time.GetUtcNow().UtcDateTime });
    }

    public Task RemoveFavoriteAsync(int userId, int gameId) => favorites.RemoveAsync(userId, gameId);
}
