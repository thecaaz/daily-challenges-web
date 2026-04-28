using DailyChallenges.Models;

namespace DailyChallenges.Repositories.Contracts
{
    public interface IUserAchievementRepository
    {
        Task<List<string>> GetUnlockedIdsAsync(int userId);
        Task<Dictionary<string, DateTime>> GetUnlockedWithTimestampsAsync(int userId);
        Task AddBatchAsync(List<UserAchievement> achievements);
    }
}
