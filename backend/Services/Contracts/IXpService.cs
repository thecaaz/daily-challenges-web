namespace DailyChallenges.Services
{
    public interface IXpService
    {
        /// <summary>
        /// Awards XP to <paramref name="userId"/> for a newly created submission.
        /// Updates User.TotalXp, User.Level, User.Streak, User.LastSubmissionAt,
        /// writes an XpEvent audit row, and stamps Submission.XpAwarded — all
        /// within the caller's ambient EF Core transaction.
        /// </summary>
        /// <returns>Amount of XP awarded (0 if user not found or already counted).</returns>
        Task<int> AwardForSubmissionAsync(int userId, int submissionId, DateTime scoringDay);

        /// <summary>
        /// Admin-level manual XP adjustment. Always inserts an audit XpEvent.
        /// </summary>
        Task<int> AdjustXpAsync(int userId, int delta, string reason, int? adminUserId = null);

        /// <summary>
        /// Awards win XP to the user for winning a scoring day on a game.
        /// </summary>
        Task<int> AwardForDayWinAsync(int userId, int gameId, DateTime scoringDay);
    }
}
