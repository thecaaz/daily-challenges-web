using System.Collections.Generic;
using System.Threading.Tasks;

namespace DailyChallenges.Services
{
    public interface IFavoriteService
    {
        Task AddFavoriteAsync(int userId, int gameId);
        Task RemoveFavoriteAsync(int userId, int gameId);
        Task<List<int>> GetFavoriteGameIdsForUserAsync(int userId);
        Task<bool> ExistsAsync(int userId, int gameId);
    }
}
