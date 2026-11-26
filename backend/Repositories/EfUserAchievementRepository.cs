using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public sealed class EfUserAchievementRepository(AppDbContext db) : IUserAchievementRepository
{
    public Task<List<string>> GetUnlockedIdsAsync(int userId, CancellationToken ct) =>
        db.UserAchievements
            .Where(ua => ua.UserId == userId)
            .Select(ua => ua.AchievementId)
            .ToListAsync(ct);

    public Task<Dictionary<string, DateTime>> GetUnlockedWithTimestampsAsync(int userId, CancellationToken ct) =>
        db.UserAchievements
            .Where(ua => ua.UserId == userId)
            .ToDictionaryAsync(ua => ua.AchievementId, ua => ua.UnlockedAt, ct);

    public async Task AddBatchAsync(List<UserAchievement> achievements, CancellationToken ct)
    {
        if (achievements.Count == 0) return;

        db.UserAchievements.AddRange(achievements);
        await db.SaveInsertsOrDetachAsync(achievements, ct);
    }
}
