using DailyChallenges.Models;
using DailyChallenges.DTOs;

namespace DailyChallenges.Repositories
{
    public interface IUserProfileRepository
    {
        Task<User?> GetByIdAsync(int userId);
        Task<List<UserGameStatDto>> GetMostPlayedGamesAsync(int userId, int topGames = 10);
    }
}
