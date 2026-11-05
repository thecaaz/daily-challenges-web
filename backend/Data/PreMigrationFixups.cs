using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DailyChallenges.Data
{
    /// <summary>
    /// Data fixes that must run before specific migrations can be applied.
    /// Only runs when the target migration is still pending, so it is a no-op on up-to-date databases.
    /// </summary>
    public static class PreMigrationFixups
    {
        public const string UniqueSubmissionMigration = "20260716165536_AddUniqueSubmissionPerUserPerGamePerDay";
        private const string ScoringDayColumnMigration = "20260329113955_ScoringDayOnSubmission";

        public static void Run(AppDbContext db, ILogger logger)
        {
            var pending = db.Database.GetPendingMigrations().ToHashSet();
            if (!pending.Contains(UniqueSubmissionMigration)) return;

            var applied = db.Database.GetAppliedMigrations().ToHashSet();
            if (!applied.Contains(ScoringDayColumnMigration)) return; // Submissions.ScoringDay doesn't exist yet; nothing to dedupe.

            using var tx = db.Database.BeginTransaction();

            // Rows that kept the 0001-01-01 default would collide with each other under the new unique index.
            // Give them an approximate day from CreatedAt instead of deleting them.
            var backfilled = db.Database.ExecuteSqlRaw(
                "UPDATE Submissions SET ScoringDay = strftime('%Y-%m-%d 00:00:00', CreatedAt) " +
                "WHERE ScoringDay IS NOT NULL AND ScoringDay < '0002-01-01'");

            // Point XP events of duplicate submissions at the kept (lowest-Id) submission of their group.
            const string duplicateFilter =
                "s.UserId IS NOT NULL AND EXISTS (SELECT 1 FROM Submissions d " +
                "WHERE d.UserId = s.UserId AND d.GameId = s.GameId AND d.ScoringDay = s.ScoringDay AND d.Id < s.Id)";

            var repointed = db.Database.ExecuteSqlRaw(
                "UPDATE XpEvents SET SubmissionId = (" +
                "  SELECT MIN(k.Id) FROM Submissions s JOIN Submissions k " +
                "  ON k.UserId = s.UserId AND k.GameId = s.GameId AND k.ScoringDay = s.ScoringDay " +
                "  WHERE s.Id = XpEvents.SubmissionId) " +
                $"WHERE SubmissionId IN (SELECT s.Id FROM Submissions s WHERE {duplicateFilter})");

            var removed = db.Database.ExecuteSqlRaw(
                $"DELETE FROM Submissions WHERE Id IN (SELECT s.Id FROM Submissions s WHERE {duplicateFilter})");

            tx.Commit();

            logger.LogWarning(
                "Pre-migration fixup for {Migration}: backfilled {Backfilled} default scoring days, repointed {Repointed} XP events, removed {Removed} duplicate submissions.",
                UniqueSubmissionMigration, backfilled, repointed, removed);
        }
    }
}
