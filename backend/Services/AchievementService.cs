using DailyChallenges.Achievements;
using DailyChallenges.Data;
using DailyChallenges.DTOs;
using DailyChallenges.Models;
using DailyChallenges.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Services
{
    public class AchievementService : IAchievementService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<AchievementService> _logger;

        public AchievementService(AppDbContext db, ILogger<AchievementService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task CheckAndAwardAsync(int userId, AchievementTrigger trigger)
        {
            try
            {
                await CheckAndAwardInternalAsync(userId, trigger);
            }
            catch (Exception ex)
            {
                // Achievement checks must never break the calling service flow.
                _logger.LogError(ex, "Achievement check failed for user {UserId} on trigger {Trigger}", userId, trigger);
            }
        }

        private async Task CheckAndAwardInternalAsync(int userId, AchievementTrigger trigger)
        {
            var alreadyUnlockedList = await _db.UserAchievements
                .Where(ua => ua.UserId == userId)
                .Select(ua => ua.AchievementId)
                .ToListAsync();

            var candidates = AchievementCatalog.All
                .Where(a => !alreadyUnlockedList.Contains(a.Id) && IsRelevant(a.Id, trigger))
                .ToList();

            if (candidates.Count == 0) return;

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return;

            var newlyUnlocked = new List<AchievementDefinition>();

            foreach (var def in candidates)
            {
                if (await IsEarnedAsync(def.Id, userId, user))
                    newlyUnlocked.Add(def);
            }

            if (newlyUnlocked.Count == 0) return;

            var now = DateTime.UtcNow;

            foreach (var def in newlyUnlocked)
            {
                _db.UserAchievements.Add(new UserAchievement
                {
                    UserId = userId,
                    AchievementId = def.Id,
                    UnlockedAt = now
                });

                _db.Notifications.Add(new Notification
                {
                    UserId = userId,
                    GameId = null,
                    ScoringDay = now.Date,
                    Message = $"Achievement unlocked: {def.Name}",
                    Type = "achievement",
                    IsRead = false,
                    CreatedAt = now
                });

                _logger.LogInformation("User {UserId} unlocked achievement {AchievementId}", userId, def.Id);
            }

            await _db.SaveChangesAsync();
        }

        public async Task<List<AchievementDto>> GetForUserAsync(int userId)
        {
            var unlocked = await _db.UserAchievements
                .Where(ua => ua.UserId == userId)
                .ToDictionaryAsync(ua => ua.AchievementId, ua => ua.UnlockedAt);

            return AchievementCatalog.All.Select(def => new AchievementDto
            {
                AchievementId = def.Id,
                Name = def.Name,
                Description = def.Description,
                IconKey = def.IconKey,
                UnlockedAt = unlocked.TryGetValue(def.Id, out var ts) ? ts : null
            }).ToList();
        }

        // ── Routing: which triggers are relevant for each achievement ────────────

        private static bool IsRelevant(string achievementId, AchievementTrigger trigger) => achievementId switch
        {
            "submission_first" or "submission_50" or "submission_250" => trigger == AchievementTrigger.Submission,
            "streak_7" or "streak_30" or "streak_100"                 => trigger == AchievementTrigger.Submission,
            "win_1" or "win_10" or "win_50"                           => trigger == AchievementTrigger.DayWin,
            "level_5" or "level_10" or "level_25"                     => trigger == AchievementTrigger.LevelUp,
            "first_friend"                                             => trigger == AchievementTrigger.FriendAccepted,
            _                                                          => false,
        };

        // ── Per-achievement evaluation ───────────────────────────────────────────

        private async Task<bool> IsEarnedAsync(string achievementId, int userId, User user)
        {
            return achievementId switch
            {
                "submission_first"  => await _db.Submissions.AnyAsync(s => s.UserId == userId),
                "submission_50"     => await _db.Submissions.CountAsync(s => s.UserId == userId) >= 50,
                "submission_250"    => await _db.Submissions.CountAsync(s => s.UserId == userId) >= 250,

                "streak_7"   => user.Streak >= 7,
                "streak_30"  => user.Streak >= 30,
                "streak_100" => user.Streak >= 100,

                "win_1"  => await CountDayWinsAsync(userId) >= 1,
                "win_10" => await CountDayWinsAsync(userId) >= 10,
                "win_50" => await CountDayWinsAsync(userId) >= 50,

                "level_5"  => user.Level >= 5,
                "level_10" => user.Level >= 10,
                "level_25" => user.Level >= 25,

                "first_friend" => await HasAcceptedFriendAsync(userId),

                _ => false,
            };
        }

        private Task<int> CountDayWinsAsync(int userId) =>
            _db.ScoringDayResults.CountAsync(r => r.WinnerUserId == userId);

        private async Task<bool> HasAcceptedFriendAsync(int userId) =>
            await _db.FriendRequests.AnyAsync(fr =>
                fr.Status == FriendRequestStatus.Accepted &&
                (fr.SenderId == userId || fr.ReceiverId == userId));
    }
}
