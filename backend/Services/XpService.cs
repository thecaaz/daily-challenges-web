using DailyChallenges.Exceptions;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using Microsoft.Extensions.Options;

namespace DailyChallenges.Services;

/// <summary>
/// XP bookkeeping. Every method reads and writes the user row, so callers run it inside a database
/// transaction (SubmissionCreationService.CreateAsync, ScoringDayFinalizerService, AdminUserService.AdjustXpAsync);
/// Microsoft.Data.Sqlite's BeginTransaction is BEGIN IMMEDIATE, which serializes these read-modify-writes.
/// The user and submission are tracked by EF, so the XpEvent insert flushes their changes too.
/// Achievement checks are the caller's job, after its commit.
/// </summary>
public sealed class XpService(
    IUserRepository users,
    ISubmissionRepository submissions,
    IXpEventRepository xpEvents,
    LevelCalculator levels,
    IOptions<XpConfig> config,
    TimeProvider time) : IXpService
{
    private const double PercentPerFraction = 100;

    private readonly XpConfig _config = config.Value;

    public async Task<int> AwardForSubmissionAsync(int userId, int submissionId, DateTime scoringDay)
    {
        var user = await users.GetByIdAsync(userId);
        if (user == null) return 0;

        var now = time.GetUtcNow().UtcDateTime;
        var streak = StreakCalculator.GetStreakAfterSubmission(user, now);
        var bonusFraction = StreakBonusFraction(streak);
        var xpAwarded = (int)Math.Round(_config.BaseXpPerSubmission * (1.0 + bonusFraction));

        AddXp(user, xpAwarded);
        user.Streak = streak;
        RecordLastSubmission(user, now);
        var submission = await StampSubmissionXpAsync(submissionId, xpAwarded);

        await xpEvents.AddAsync(new XpEvent
        {
            UserId = userId,
            SubmissionId = submissionId,
            GameId = submission?.GameId,
            ScoringDay = scoringDay.Date,
            Amount = xpAwarded,
            EventType = XpEventTypes.Submission,
            Details = DescribeStreakBonus(streak, bonusFraction),
            CreatedAt = now
        });

        return xpAwarded;
    }

    public async Task<int> AdjustXpAsync(XpAdjustment adjustment)
    {
        var (userId, delta, reason, adminUserId) = adjustment;
        var user = await users.GetByIdAsync(userId)
            ?? throw new NotFoundException($"User {userId} not found");

        var applied = ApplyClampedDelta(user, delta);

        var details = $"requested={delta},reason={reason}" +
                      (adminUserId.HasValue ? $",admin={adminUserId}" : string.Empty);

        await xpEvents.AddAsync(new XpEvent
        {
            UserId = userId,
            Amount = applied,
            EventType = XpEventTypes.AdminAdjustment,
            Details = details,
            CreatedAt = time.GetUtcNow().UtcDateTime
        });

        return applied;
    }

    public async Task<int> RevokeSubmissionAsync(SubmissionXpRevocation revocation)
    {
        var (userId, submissionId, gameId, scoringDay, amount, reason) = revocation;
        if (amount <= 0) return 0;
        var user = await users.GetByIdAsync(userId);
        if (user == null) return 0;

        var applied = ApplyDeduction(user, amount);

        await xpEvents.AddAsync(new XpEvent
        {
            UserId = userId,
            SubmissionId = submissionId,
            GameId = gameId,
            ScoringDay = scoringDay.Date,
            Amount = -applied,
            EventType = XpEventTypes.SubmissionRevoked,
            Details = DescribeRevocation(submissionId, amount, reason),
            CreatedAt = time.GetUtcNow().UtcDateTime
        });

        return applied;
    }

    /// <summary>The event is deleted rather than offset, so the unique day_win index lets the day be won again.</summary>
    public async Task<int> RevokeDayWinAsync(int userId, int gameId, DateTime scoringDay)
    {
        var dayWin = await xpEvents.GetDayWinAsync(userId, gameId, scoringDay.Date);
        if (dayWin == null) return 0;

        var user = await users.GetByIdAsync(userId);
        if (user != null) ApplyDeduction(user, dayWin.Amount);

        await xpEvents.RemoveAsync(dayWin);
        return dayWin.Amount;
    }

    /// <summary>Idempotent: a day is won at most once (also enforced by a filtered unique index).</summary>
    public async Task<int> AwardForDayWinAsync(int userId, int gameId, DateTime scoringDay)
    {
        var scoringDate = scoringDay.Date;
        if (await xpEvents.HasDayWinAsync(userId, gameId, scoringDate)) return 0;

        var user = await users.GetByIdAsync(userId);
        if (user == null) return 0;

        var xpAwarded = AddXp(user, _config.WinXp);

        await xpEvents.AddAsync(new XpEvent
        {
            UserId = userId,
            GameId = gameId,
            ScoringDay = scoringDate,
            Amount = xpAwarded,
            EventType = XpEventTypes.DayWin,
            Details = $"win_xp={xpAwarded}",
            CreatedAt = time.GetUtcNow().UtcDateTime
        });

        return xpAwarded;
    }

    private double StreakBonusFraction(int streak) =>
        Math.Min(Math.Max(streak - 1, 0) * _config.StreakBonusPerDay, _config.MaxStreakBonus);

    private static string DescribeStreakBonus(int streak, double bonusFraction) =>
        streak > 1
            ? $"streak={streak},bonus={bonusFraction * PercentPerFraction:F0}%"
            : "streak=1,bonus=0%";

    /// <summary>SubmissionId is set to null when the submission row is deleted, so the id is kept in Details too.</summary>
    private static string DescribeRevocation(int submissionId, int amount, string? reason) =>
        $"submissionId={submissionId},requested=-{amount}" + (string.IsNullOrEmpty(reason) ? string.Empty : $",reason={reason}");

    /// <summary>"Last played" keeps the full timestamp; the streak rules compare only its date.</summary>
    private static void RecordLastSubmission(User user, DateTime now)
    {
        if (user.LastSubmissionAt == null || now > user.LastSubmissionAt.Value)
            user.LastSubmissionAt = now;
    }

    private async Task<Submission?> StampSubmissionXpAsync(int submissionId, int xpAwarded)
    {
        var submission = await submissions.GetByIdAsync(submissionId);
        if (submission != null)
            submission.XpAwarded = xpAwarded;
        return submission;
    }

    private int AddXp(User user, int amount)
    {
        user.TotalXp += amount;
        user.Level = levels.GetLevelFromTotalXp(user.TotalXp);
        return amount;
    }

    /// <summary>TotalXp never goes below 0, so the applied delta can be smaller than the requested one.</summary>
    private int ApplyClampedDelta(User user, int delta)
    {
        var before = user.TotalXp;
        user.TotalXp = Math.Max(0, user.TotalXp + delta);
        user.Level = levels.GetLevelFromTotalXp(user.TotalXp);
        return (int)(user.TotalXp - before);
    }

    /// <summary>Subtracts up to <paramref name="amount"/> from TotalXp (clamped at 0), re-derives Level; returns the amount removed.</summary>
    private int ApplyDeduction(User user, int amount)
    {
        var before = user.TotalXp;
        user.TotalXp = Math.Max(0, user.TotalXp - Math.Max(0, amount));
        user.Level = levels.GetLevelFromTotalXp(user.TotalXp);
        return (int)(before - user.TotalXp);
    }
}
