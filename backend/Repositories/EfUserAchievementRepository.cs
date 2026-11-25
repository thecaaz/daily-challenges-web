using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public sealed class EfUserAchievementRepository(AppDbContext db) : IUserAchievementRepository
{
    public Task<List<string>> GetUnlockedIdsAsync(int userId) =>
        db.UserAchievements
            .Where(ua => ua.UserId == userId)
            .Select(ua => ua.AchievementId)
            .ToListAsync();

    public Task<Dictionary<string, DateTime>> GetUnlockedWithTimestampsAsync(int userId) =>
        db.UserAchievements
            .Where(ua => ua.UserId == userId)
            .ToDictionaryAsync(ua => ua.AchievementId, ua => ua.UnlockedAt);

    public async Task AddBatchAsync(List<UserAchievement> achievements)
    {
        if (achievements.Count == 0) return;

        db.UserAchievements.AddRange(achievements);
        await db.SaveInsertsOrDetachAsync(achievements);
    }
}
