using DailyChallenges.Models;
namespace DailyChallenges.Repositories
{
    public interface IFavoriteRepository
    {
        Task<bool> ExistsAsync(int userId, int gameId);
        Task AddAsync(Favorite favorite);
        Task RemoveAsync(int userId, int gameId);
        Task<List<int>> GetFavoriteGameIdsForUserAsync(int userId);
    }
}
