using DailyChallenges.Models;
using DailyChallenges.DTOs;

namespace DailyChallenges.Repositories;

public interface IUserProfileRepository
{
    Task<User?> GetByIdAsync(int userId, CancellationToken ct);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct);
    /// <summary>
    /// Per-game stats of the user, the <paramref name="topGames"/> most played (10 when below 1).
    /// Submissions in <paramref name="excludeSubmissionIds"/> are not counted.
    /// </summary>
    Task<List<UserGameStatDto>> GetMostPlayedGamesAsync(int userId, int topGames, IReadOnlyCollection<int>? excludeSubmissionIds, CancellationToken ct);
}
