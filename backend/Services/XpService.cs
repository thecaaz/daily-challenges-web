using DailyChallenges.Data;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
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
        private readonly AppDbContext _db;
        private readonly LevelCalculator _levelCalc;
        private readonly XpConfig _cfg;

        public XpService(AppDbContext db, LevelCalculator levelCalc, IOptions<XpConfig> cfg)
        {
            _db = db;
            _levelCalc = levelCalc;
            _cfg = cfg.Value;
        }

        public async Task<int> AwardForSubmissionAsync(int userId, int submissionId, DateTime scoringDay)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return 0;

            var scoringDate = scoringDay.Date;

            // --- Streak calculation ---
            int newStreak;
            if (user.LastSubmissionAt == null)
            {
                // Very first submission ever.
                newStreak = 1;
            }
            else
            {
                var lastDate = user.LastSubmissionAt.Value.Date;

                if (scoringDate == lastDate)
                {
                    // Same scoring day (different game): award XP but keep the existing streak
                    // unchanged — the streak has already been accounted for this day.
                    newStreak = user.Streak;
                }
                else if (scoringDate == lastDate.AddDays(1))
                {
                    // Consecutive day — extend streak.
                    newStreak = user.Streak + 1;
                }
                else
                {
                    // Gap detected — reset streak.
                    newStreak = 1;
                }
            }

            // --- Bonus calculation ---
            // Bonus starts at 0% for streak=1 (no prior day) and grows 1% per consecutive day
            // beyond the first, capped at MaxStreakBonus.
            double bonusFraction = Math.Min(Math.Max(newStreak - 1, 0) * _cfg.StreakBonusPerDay, _cfg.MaxStreakBonus);
            int xpAwarded = (int)Math.Round(_cfg.BaseXpPerSubmission * (1.0 + bonusFraction));

            // --- Update user ---
            user.TotalXp += xpAwarded;
            user.Level = _levelCalc.GetLevelFromTotalXp(user.TotalXp);
            user.Streak = newStreak;
            // Only advance LastSubmissionAt when the scoring day is newer, so that same-day
            // submissions for different games do not accidentally re-trigger streak logic.
            if (user.LastSubmissionAt == null || scoringDate > user.LastSubmissionAt.Value.Date)
                user.LastSubmissionAt = scoringDate;

            // --- Stamp submission ---
            var submission = await _db.Submissions.FindAsync(submissionId);
            if (submission != null)
                submission.XpAwarded = xpAwarded;

            // --- Audit event ---
            var details = newStreak > 1
                ? $"streak={newStreak},bonus={bonusFraction * 100:F0}%"
                : "streak=1,bonus=0%";

            _db.XpEvents.Add(new XpEvent
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

            await _db.SaveChangesAsync();
            return xpAwarded;
        }

        public async Task<int> AdjustXpAsync(int userId, int delta, string reason, int? adminUserId = null)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) throw new KeyNotFoundException($"User {userId} not found");

            user.TotalXp = Math.Max(0, user.TotalXp + delta);
            user.Level = _levelCalc.GetLevelFromTotalXp(user.TotalXp);

            var details = $"delta={delta},reason={reason}" +
                          (adminUserId.HasValue ? $",admin={adminUserId}" : string.Empty);

            _db.XpEvents.Add(new XpEvent
            {
                UserId = userId,
                Amount = delta,
                EventType = "admin_adjustment",
                Details = details,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return delta;
        }

        public async Task<int> AwardForDayWinAsync(int userId, int gameId, DateTime scoringDay)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return 0;

            var scoringDate = scoringDay.Date;
            int xpAwarded = _cfg.WinXp;

            user.TotalXp += xpAwarded;
            user.Level = _levelCalc.GetLevelFromTotalXp(user.TotalXp);

            _db.XpEvents.Add(new XpEvent
            {
                UserId = userId,
                GameId = gameId,
                ScoringDay = scoringDate,
                Amount = xpAwarded,
                EventType = "day_win",
                Details = $"win_xp={xpAwarded}",
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return xpAwarded;
        }
    }
}
