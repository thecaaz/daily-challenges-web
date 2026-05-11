using DailyChallenges.Achievements;
using DailyChallenges.DTOs;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Services
{
    public class AchievementService : IAchievementService
    {
        private readonly IUserAchievementRepository _userAchievementRepo;
        private readonly IUserRepository _userRepo;
        private readonly ISubmissionRepository _submissionRepo;
        private readonly IScoringDayResultRepository _scoringDayResultRepo;
        private readonly INotificationRepository _notificationRepo;
        private readonly IFriendRepository _friendRepo;
        private readonly ILogger<AchievementService> _logger;

        public AchievementService(
            IUserAchievementRepository userAchievementRepo,
            IUserRepository userRepo,
            ISubmissionRepository submissionRepo,
            IScoringDayResultRepository scoringDayResultRepo,
            INotificationRepository notificationRepo,
            IFriendRepository friendRepo,
            ILogger<AchievementService> logger)
        {
            _userAchievementRepo = userAchievementRepo;
            _userRepo = userRepo;
            _submissionRepo = submissionRepo;
            _scoringDayResultRepo = scoringDayResultRepo;
            _notificationRepo = notificationRepo;
            _friendRepo = friendRepo;
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
                _logger.LogError(ex, "Achievement check failed for user {UserId} on trigger {Trigger}", userId, trigger);
            }
        }

        private async Task CheckAndAwardInternalAsync(int userId, AchievementTrigger trigger)
        {
            var alreadyUnlockedIds = await _userAchievementRepo.GetUnlockedIdsAsync(userId);

            var candidates = AchievementCatalog.All
                .Where(a => !alreadyUnlockedIds.Contains(a.Id) && IsRelevant(a.Id, trigger))
                .ToList();

            if (candidates.Count == 0) return;

            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null) return;

            var newlyUnlocked = new List<AchievementDefinition>();
            foreach (var def in candidates)
            {
                if (await IsEarnedAsync(def.Id, userId, user))
                    newlyUnlocked.Add(def);
            }

            if (newlyUnlocked.Count == 0) return;

            var now = DateTime.UtcNow;

            var newAchievements = newlyUnlocked.Select(def => new UserAchievement
            {
                UserId = userId,
                AchievementId = def.Id,
                UnlockedAt = now
            }).ToList();

            var notifications = newlyUnlocked.Select(def => new Notification
            {
                UserId = userId,
                GameId = null,
                ScoringDay = now.Date,
                Message = $"Achievement unlocked: {def.Name}",
                Type = "achievement",
                IsRead = false,
                CreatedAt = now
            }).ToList();

            foreach (var def in newlyUnlocked)
                _logger.LogInformation("User {UserId} unlocked achievement {AchievementId}", userId, def.Id);

            await _userAchievementRepo.AddBatchAsync(newAchievements);
            await _notificationRepo.CreateBatchAsync(notifications);
        }

        public async Task<List<AchievementDto>> GetForUserAsync(int userId)
        {
            var unlocked = await _userAchievementRepo.GetUnlockedWithTimestampsAsync(userId);

            return AchievementCatalog.All.Select(def => new AchievementDto
            {
                AchievementId = def.Id,
                Name = def.Name,
                Description = def.Description,
                IconKey = def.IconKey,
                UnlockedAt = unlocked.TryGetValue(def.Id, out var ts) ? ts : null
            }).ToList();
        }

        private static bool IsRelevant(string achievementId, AchievementTrigger trigger) => achievementId switch
        {
            "submission_first" or "submission_50" or "submission_250" => trigger == AchievementTrigger.Submission,
            "streak_7" or "streak_30" or "streak_100"                 => trigger == AchievementTrigger.Submission,
            "win_1" or "win_10" or "win_50"                           => trigger == AchievementTrigger.DayWin,
            "level_5" or "level_10" or "level_25"                     => trigger == AchievementTrigger.LevelUp,
            "first_friend"                                             => trigger == AchievementTrigger.FriendAccepted,
            _                                                          => false,
        };

        private async Task<bool> IsEarnedAsync(string achievementId, int userId, User user)
        {
            return achievementId switch
            {
                "submission_first"  => await _submissionRepo.CountByUserAsync(userId) >= 1,
                "submission_50"     => await _submissionRepo.CountByUserAsync(userId) >= 50,
                "submission_250"    => await _submissionRepo.CountByUserAsync(userId) >= 250,

                "streak_7"   => user.Streak >= 7,
                "streak_30"  => user.Streak >= 30,
                "streak_100" => user.Streak >= 100,

                "win_1"  => await _scoringDayResultRepo.CountWinsByUserAsync(userId) >= 1,
                "win_10" => await _scoringDayResultRepo.CountWinsByUserAsync(userId) >= 10,
                "win_50" => await _scoringDayResultRepo.CountWinsByUserAsync(userId) >= 50,

                "level_5"  => user.Level >= 5,
                "level_10" => user.Level >= 10,
                "level_25" => user.Level >= 25,

                "first_friend" => await _friendRepo.HasAcceptedFriendAsync(userId),

                _ => false,
            };
        }
    }
}
