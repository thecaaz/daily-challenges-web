namespace DailyChallenges.Services
{
    /// <summary>
    /// XP bookkeeping. Each method reads and updates the user row, so it must run inside the caller's
    /// database transaction (BEGIN IMMEDIATE on SQLite), with the user not loaded before the transaction
    /// began; otherwise concurrent awards can overwrite each other. Achievement checks (LevelUp etc.) are
    /// left to the caller, after its commit.
    /// </summary>
    public interface IXpService
    {
        /// <summary>
        /// Awards XP to <paramref name="userId"/> for a newly created submission.
        /// Updates User.TotalXp, User.Level, User.Streak and User.LastSubmissionAt (full UTC timestamp),
        /// stamps Submission.XpAwarded and writes a "submission" XpEvent (the streak bonus is part of its amount).
        /// </summary>
        /// <returns>Amount of XP awarded (0 if the user was not found).</returns>
        Task<int> AwardForSubmissionAsync(int userId, int submissionId, DateTime scoringDay);

        /// <summary>
        /// Admin-level manual XP adjustment. TotalXp is clamped at 0; the "admin_adjustment" XpEvent records the
        /// applied delta as Amount and the requested delta in Details.
        /// </summary>
        /// <returns>The delta actually applied.</returns>
        Task<int> AdjustXpAsync(int userId, int delta, string reason, int? adminUserId = null);

        /// <summary>
        /// Awards win XP for winning a scoring day on a game. Idempotent: returns 0 without changes when a
        /// "day_win" event already exists for that user, game and day.
        /// </summary>
        Task<int> AwardForDayWinAsync(int userId, int gameId, DateTime scoringDay);

        /// <summary>
        /// Takes back the XP of a submission that is being deleted: subtracts <paramref name="amount"/> from TotalXp
        /// (clamped at 0), re-derives Level and writes a negative "submission_revoked" XpEvent with the applied amount.
        /// Streak and LastSubmissionAt are left alone.
        /// </summary>
        /// <returns>The XP actually removed.</returns>
        Task<int> RevokeSubmissionAsync(int userId, int submissionId, int gameId, DateTime scoringDay, int amount, string? reason = null);

        /// <summary>
        /// Takes back a day win: deletes the user's "day_win" XpEvent for the game and day and subtracts its amount
        /// from TotalXp (clamped at 0). Deleting (not offsetting) the event lets the finalizer award the day again.
        /// </summary>
        /// <returns>The amount of the removed event (0 when there was none).</returns>
        Task<int> RevokeDayWinAsync(int userId, int gameId, DateTime scoringDay);
    }
}
