using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
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

    /// <summary>
    /// XP bookkeeping. Every method reads and writes the user row, so callers run it inside a database
    /// transaction (SubmissionService.CreateAsync, ScoringDayFinalizerService, AdminUserService.AdjustXpAsync);
    /// Microsoft.Data.Sqlite's BeginTransaction is BEGIN IMMEDIATE, which serializes these read-modify-writes.
    /// Achievement checks are the caller's job, after its commit.
    /// </summary>
    public class XpService : IXpService
    {
        private readonly IUserRepository _userRepo;
        private readonly ISubmissionRepository _submissionRepo;
        private readonly IXpEventRepository _xpEventRepo;
        private readonly LevelCalculator _levelCalc;
        private readonly XpConfig _cfg;
        private readonly TimeProvider _time;

        public XpService(IUserRepository userRepo, ISubmissionRepository submissionRepo, IXpEventRepository xpEventRepo, LevelCalculator levelCalc, IOptions<XpConfig> cfg, TimeProvider time)
        {
            _userRepo = userRepo;
            _submissionRepo = submissionRepo;
            _xpEventRepo = xpEventRepo;
            _levelCalc = levelCalc;
            _cfg = cfg.Value;
            _time = time;
        }

        public async Task<int> AwardForSubmissionAsync(int userId, int submissionId, DateTime scoringDay)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null) return 0;

            var scoringDate = scoringDay.Date;
            var now = _time.GetUtcNow().UtcDateTime;
            var today = now.Date;

            // --- Streak calculation (UTC calendar days; only the date part of LastSubmissionAt counts) ---
            int newStreak;
            if (user.LastSubmissionAt == null)
            {
                newStreak = 1;
            }
            else
            {
                var lastDate = user.LastSubmissionAt.Value.Date;

                if (today == lastDate)
                {
                    newStreak = Math.Max(user.Streak, 1);
                }
                else if (today == lastDate.AddDays(1))
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
            // Full timestamp, so "last played" is accurate; the streak logic above compares only the date.
            if (user.LastSubmissionAt == null || now > user.LastSubmissionAt.Value)
                user.LastSubmissionAt = now;

            // --- Stamp submission XpAwarded (tracked by EF) ---
            var submission = await _submissionRepo.GetByIdAsync(submissionId);
            if (submission != null)
                submission.XpAwarded = xpAwarded;

            // --- Audit event; AddAsync flushes all pending tracked changes ---
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
                CreatedAt = now
            });

            return xpAwarded;
        }

        public async Task<int> AdjustXpAsync(int userId, int delta, string reason, int? adminUserId = null)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null) throw new KeyNotFoundException($"User {userId} not found");

            // TotalXp never goes below 0, so the applied delta can be smaller than the requested one.
            var before = user.TotalXp;
            user.TotalXp = Math.Max(0, user.TotalXp + delta);
            user.Level = _levelCalc.GetLevelFromTotalXp(user.TotalXp);
            var applied = (int)(user.TotalXp - before);

            var details = $"requested={delta},reason={reason}" +
                          (adminUserId.HasValue ? $",admin={adminUserId}" : string.Empty);

            await _xpEventRepo.AddAsync(new XpEvent
            {
                UserId = userId,
                Amount = applied,
                EventType = "admin_adjustment",
                Details = details,
                CreatedAt = _time.GetUtcNow().UtcDateTime
            });

            return applied;
        }

        public async Task<int> RevokeSubmissionAsync(int userId, int submissionId, int gameId, DateTime scoringDay, int amount, string? reason = null)
        {
            if (amount <= 0) return 0;
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null) return 0;

            var applied = ApplyDeduction(user, amount);

            // SubmissionId is set to null when the submission row is deleted, so the id is kept in Details too.
            await _xpEventRepo.AddAsync(new XpEvent
            {
                UserId = userId,
                SubmissionId = submissionId,
                GameId = gameId,
                ScoringDay = scoringDay.Date,
                Amount = -applied,
                EventType = "submission_revoked",
                Details = $"submissionId={submissionId},requested=-{amount}" + (string.IsNullOrEmpty(reason) ? string.Empty : $",reason={reason}"),
                CreatedAt = _time.GetUtcNow().UtcDateTime
            });

            return applied;
        }

        public async Task<int> RevokeDayWinAsync(int userId, int gameId, DateTime scoringDay)
        {
            var ev = await _xpEventRepo.GetDayWinAsync(userId, gameId, scoringDay.Date);
            if (ev == null) return 0;

            var user = await _userRepo.GetByIdAsync(userId);
            if (user != null) ApplyDeduction(user, ev.Amount);

            // Deleted rather than offset, so the unique day_win index lets the day be won again.
            await _xpEventRepo.RemoveAsync(ev);
            return ev.Amount;
        }

        /// <summary>Subtracts up to <paramref name="amount"/> from TotalXp (clamped at 0), re-derives Level; returns the amount removed.</summary>
        private int ApplyDeduction(User user, int amount)
        {
            var before = user.TotalXp;
            user.TotalXp = Math.Max(0, user.TotalXp - Math.Max(0, amount));
            user.Level = _levelCalc.GetLevelFromTotalXp(user.TotalXp);
            return (int)(before - user.TotalXp);
        }

        public async Task<int> AwardForDayWinAsync(int userId, int gameId, DateTime scoringDay)
        {
            var scoringDate = scoringDay.Date;

            // Idempotent: a day is won at most once (also enforced by a filtered unique index).
            if (await _xpEventRepo.HasDayWinAsync(userId, gameId, scoringDate)) return 0;

            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null) return 0;

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
                CreatedAt = _time.GetUtcNow().UtcDateTime
            });

            return xpAwarded;
        }
    }
}
