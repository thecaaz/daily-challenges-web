using DailyChallenges.Models;

namespace DailyChallenges.Repositories.Contracts;

public interface IUserAchievementRepository
{
    Task<List<string>> GetUnlockedIdsAsync(int userId, CancellationToken ct);
    Task<Dictionary<string, DateTime>> GetUnlockedWithTimestampsAsync(int userId, CancellationToken ct);
    Task AddBatchAsync(List<UserAchievement> achievements, CancellationToken ct);
}
