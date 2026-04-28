using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories
{
    public class EfUserAchievementRepository : IUserAchievementRepository
    {
        private readonly AppDbContext _db;
        public EfUserAchievementRepository(AppDbContext db) => _db = db;

        public async Task<List<string>> GetUnlockedIdsAsync(int userId) =>
            await _db.UserAchievements
                .Where(ua => ua.UserId == userId)
                .Select(ua => ua.AchievementId)
                .ToListAsync();

        public async Task<Dictionary<string, DateTime>> GetUnlockedWithTimestampsAsync(int userId) =>
            await _db.UserAchievements
                .Where(ua => ua.UserId == userId)
                .ToDictionaryAsync(ua => ua.AchievementId, ua => ua.UnlockedAt);

        public async Task AddBatchAsync(List<UserAchievement> achievements)
        {
            if (achievements.Count == 0) return;
            _db.UserAchievements.AddRange(achievements);
            await _db.SaveChangesAsync();
        }
    }
}
