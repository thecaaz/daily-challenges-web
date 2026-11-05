using DailyChallenges.Models;

namespace DailyChallenges.Services
{
    /// <summary>
    /// The streak shown to users. <see cref="User.Streak"/> is only updated when the user submits, so a user
    /// who stopped playing keeps their old value in the database; the effective streak is 0 once the last
    /// submission is older than yesterday (UTC dates, the same days the XP streak logic uses).
    /// </summary>
    public static class StreakHelper
    {
        public static int Effective(User user, DateTime todayUtc)
            => Effective(user.Streak, user.LastSubmissionAt, todayUtc);

        public static int Effective(int streak, DateTime? lastSubmissionAt, DateTime todayUtc)
        {
            if (lastSubmissionAt == null) return 0;
            return lastSubmissionAt.Value.Date < todayUtc.Date.AddDays(-1) ? 0 : streak;
        }
    }
}
