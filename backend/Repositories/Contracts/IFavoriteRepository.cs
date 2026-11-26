using DailyChallenges.Models;
namespace DailyChallenges.Repositories;

public interface IFavoriteRepository
{
    Task<bool> ExistsAsync(int userId, int gameId, CancellationToken ct);
    Task AddAsync(Favorite favorite, CancellationToken ct);
    Task RemoveAsync(int userId, int gameId, CancellationToken ct);
    Task<List<int>> GetFavoriteGameIdsForUserAsync(int userId, CancellationToken ct);
}
