using DailyChallenges.Models;

namespace DailyChallenges.Services;

/// <summary>
/// Submission streak rules. Streak days are UTC calendar dates; only the date part of
/// <see cref="User.LastSubmissionAt"/> counts.
/// </summary>
public static class StreakCalculator
{
    /// <summary>
    /// The streak shown to users. <see cref="User.Streak"/> is only updated when the user submits, so a user
    /// who stopped playing keeps their old value in the database; the effective streak is 0 once the last
    /// submission is older than yesterday.
    /// </summary>
    public static int GetEffectiveStreak(User user, DateTime todayUtc)
    {
        if (user.LastSubmissionAt is not DateTime lastSubmissionAt) return 0;
        return lastSubmissionAt.Date < todayUtc.Date.AddDays(-1) ? 0 : user.Streak;
    }

    /// <summary>
    /// The streak after a submission today: unchanged (at least 1) for another submission on the same day,
    /// one longer after a submission yesterday, otherwise a fresh streak of 1.
    /// </summary>
    public static int GetStreakAfterSubmission(User user, DateTime todayUtc)
    {
        if (user.LastSubmissionAt is not DateTime lastSubmissionAt) return 1;

        var today = todayUtc.Date;
        var lastSubmissionDay = lastSubmissionAt.Date;
        if (today == lastSubmissionDay) return Math.Max(user.Streak, 1);
        if (today == lastSubmissionDay.AddDays(1)) return user.Streak + 1;
        return 1;
    }
}
