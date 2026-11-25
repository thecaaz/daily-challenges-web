using DailyChallenges.Models;
using DailyChallenges.DTOs;

namespace DailyChallenges.Repositories;

public interface IUserProfileRepository
{
    Task<User?> GetByIdAsync(int userId);
    Task<User?> GetByUsernameAsync(string username);
    /// <summary>
    /// Per-game stats of the user, the <paramref name="topGames"/> most played (10 when below 1).
    /// Submissions in <paramref name="excludeSubmissionIds"/> are not counted.
    /// </summary>
    Task<List<UserGameStatDto>> GetMostPlayedGamesAsync(int userId, int topGames, IReadOnlyCollection<int>? excludeSubmissionIds);
}
