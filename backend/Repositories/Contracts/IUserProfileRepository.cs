using DailyChallenges.Models;
using DailyChallenges.DTOs;

namespace DailyChallenges.Repositories
{
    public interface IUserProfileRepository
    {
        Task<User?> GetByIdAsync(int userId);
        Task<User?> GetByUsernameAsync(string username);
        /// <summary>Per-game stats of the user. Submissions in <paramref name="excludeSubmissionIds"/> are not counted.</summary>
        Task<List<UserGameStatDto>> GetMostPlayedGamesAsync(int userId, int topGames = 10, IReadOnlyCollection<int>? excludeSubmissionIds = null);
    }
}
