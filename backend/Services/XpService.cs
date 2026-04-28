using DailyChallenges.Achievements;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Contracts;
using Microsoft.Extensions.Options;

namespace DailyChallenges.Services
{
    public class XpConfig
    {
        public int BaseXpPerSubmission { get; set; } = 50;
        /// <summary>Fraction added per streak day beyond the first (default 1 %).</summary>
        public double StreakBonusPerDay { get; set; } = 0.01;
        /// <summary>Maximum streak bonus fraction (default 30 % = 30-day cap).</summary>
        public double MaxStreakBonus { get; set; } = 0.30;
        public double LevelBase { get; set; } = 100;
        public double LevelExponent { get; set; } = 1.5;
        /// <summary>XP awarded for winning a scoring day (default 100).</summary>
        public int WinXp { get; set; } = 100;
    }

    public class XpService : IXpService
    {
        private readonly IUserRepository _userRepo;
        private readonly ISubmissionRepository _submissionRepo;
        private readonly IXpEventRepository _xpEventRepo;
        private readonly LevelCalculator _levelCalc;
        private readonly XpConfig _cfg;
        private readonly IAchievementService _achievements;

        public XpService(IUserRepository userRepo, ISubmissionRepository submissionRepo, IXpEventRepository xpEventRepo, LevelCalculator levelCalc, IOptions<XpConfig> cfg, IAchievementService achievements)
        {
            _userRepo = userRepo;
            _submissionRepo = submissionRepo;
            _xpEventRepo = xpEventRepo;
            _levelCalc = levelCalc;
            _cfg = cfg.Value;
            _achievements = achievements;
        }

        public async Task<int> AwardForSubmissionAsync(int userId, int submissionId, DateTime scoringDay)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null) return 0;

            var scoringDate = scoringDay.Date;

            // --- Streak calculation ---
            int newStreak;
            if (user.LastSubmissionAt == null)
            {
                newStreak = 1;
            }
            else
            {
                var lastDate = user.LastSubmissionAt.Value.Date;

                if (scoringDate == lastDate)
                {
                    // Same scoring day (different game): award XP but keep streak unchanged.
                    newStreak = user.Streak;
                }
                else if (scoringDate == lastDate.AddDays(1))
                {
                    newStreak = user.Streak + 1;
                }
                else
                {
                    newStreak = 1;
                }
            }

            // --- Bonus calculation ---
            double bonusFraction = Math.Min(Math.Max(newStreak - 1, 0) * _cfg.StreakBonusPerDay, _cfg.MaxStreakBonus);
            int xpAwarded = (int)Math.Round(_cfg.BaseXpPerSubmission * (1.0 + bonusFraction));

            // --- Update user (tracked by EF; changes saved by AddAsync below) ---
            user.TotalXp += xpAwarded;
            user.Level = _levelCalc.GetLevelFromTotalXp(user.TotalXp);
            user.Streak = newStreak;
            if (user.LastSubmissionAt == null || scoringDate > user.LastSubmissionAt.Value.Date)
                user.LastSubmissionAt = scoringDate;

            // --- Stamp submission XpAwarded (tracked by EF) ---
            var submission = await _submissionRepo.GetByIdAsync(submissionId);
            if (submission != null)
                submission.XpAwarded = xpAwarded;

            // --- Audit event; AddAsync flushes all pending tracked changes atomically ---
            var details = newStreak > 1
                ? $"streak={newStreak},bonus={bonusFraction * 100:F0}%"
                : "streak=1,bonus=0%";

            await _xpEventRepo.AddAsync(new XpEvent
            {
                UserId = userId,
                SubmissionId = submissionId,
                GameId = submission?.GameId,
                ScoringDay = scoringDate,
                Amount = xpAwarded,
                EventType = "submission",
                Details = details,
                CreatedAt = DateTime.UtcNow
            });

            // Check level-based achievements after the XP/level are persisted.
            await _achievements.CheckAndAwardAsync(userId, AchievementTrigger.LevelUp);

            return xpAwarded;
        }

        public async Task<int> AdjustXpAsync(int userId, int delta, string reason, int? adminUserId = null)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null) throw new KeyNotFoundException($"User {userId} not found");

            user.TotalXp = Math.Max(0, user.TotalXp + delta);
            user.Level = _levelCalc.GetLevelFromTotalXp(user.TotalXp);

            var details = $"delta={delta},reason={reason}" +
                          (adminUserId.HasValue ? $",admin={adminUserId}" : string.Empty);

            await _xpEventRepo.AddAsync(new XpEvent
            {
                UserId = userId,
                Amount = delta,
                EventType = "admin_adjustment",
                Details = details,
                CreatedAt = DateTime.UtcNow
            });

            return delta;
        }

        public async Task<int> AwardForDayWinAsync(int userId, int gameId, DateTime scoringDay)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null) return 0;

            var scoringDate = scoringDay.Date;
            int xpAwarded = _cfg.WinXp;

            user.TotalXp += xpAwarded;
            user.Level = _levelCalc.GetLevelFromTotalXp(user.TotalXp);

            await _xpEventRepo.AddAsync(new XpEvent
            {
                UserId = userId,
                GameId = gameId,
                ScoringDay = scoringDate,
                Amount = xpAwarded,
                EventType = "day_win",
                Details = $"win_xp={xpAwarded}",
                CreatedAt = DateTime.UtcNow
            });

            return xpAwarded;
        }
    }
}
