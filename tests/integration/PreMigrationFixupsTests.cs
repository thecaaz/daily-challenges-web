using DailyChallenges.Data;
using DailyChallenges.IntegrationTests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DailyChallenges.IntegrationTests;

public class PreMigrationFixupsTests
{
    private const string MigrationBeforeUniqueIndex = "20260429182116_AddLeagues";

    private const string SubmissionColumns =
        "INSERT INTO Submissions (Id, GameId, UserId, Score, ScreenshotData, ScreenshotContentType, CreatedAt, ScoringDay) VALUES ";

    /// <summary>Submissions 10 and 11: the same user, game and day (race duplicates).</summary>
    private const string RaceDuplicateSubmissions =
        " (10, 1, 1, '5', x'00', 'image/png', '2026-07-01 10:00:00', '2026-07-01 00:00:00')," +
        " (11, 1, 1, '6', x'00', 'image/png', '2026-07-01 10:00:01', '2026-07-01 00:00:00')";

    /// <summary>Submissions 20 and 21: kept the 0001-01-01 default scoring day but were made on different days.</summary>
    private const string DefaultScoringDaySubmissionsFromDifferentDays =
        " (20, 1, 1, '7', x'00', 'image/png', '2026-03-01 10:00:00', '0001-01-01 00:00:00')," +
        " (21, 1, 1, '8', x'00', 'image/png', '2026-03-02 10:00:00', '0001-01-01 00:00:00')";

    [Fact]
    public void Removes_duplicates_so_unique_submission_migration_applies()
    {
        using var db = MigrationTestDb.MigratedTo(MigrationBeforeUniqueIndex);
        db.ExecuteSql(
            "INSERT INTO Users (Id, Username, PasswordHash, IsAdmin, TotalXp, Level, Streak) VALUES (1, 'alice', 'x', 0, 0, 1, 0);" +
            "INSERT INTO Games (Id, Name, ResetTime, RankingMode) VALUES (1, 'Game', '00:00:00', 0);" +
            SubmissionColumns + RaceDuplicateSubmissions + "," + DefaultScoringDaySubmissionsFromDifferentDays + ";" +
            "INSERT INTO XpEvents (UserId, EventType, Amount, CreatedAt, SubmissionId) VALUES (1, 'submission', 10, '2026-07-01 10:00:01', 11);");

        PreMigrationFixups.Run(db.Context, NullLogger.Instance);
        db.MigrateToLatest();

        Assert.Empty(db.Context.Database.GetPendingMigrations());
        var ids = db.Context.Submissions.Select(s => s.Id).OrderBy(i => i).ToList();
        Assert.Equal(new[] { 10, 20, 21 }, ids);
        Assert.Equal(10, db.Context.XpEvents.Single().SubmissionId);
    }

    /// <summary>
    /// Also the "Migrate() works on a fresh database" test: same calls Program.cs makes at startup,
    /// against a brand-new SQLite file rather than an in-memory database.
    /// </summary>
    [Fact]
    public void Is_noop_on_fresh_database_and_all_migrations_apply()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dc-migrate-{Guid.NewGuid():N}.db");
        try
        {
            using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path}").Options))
            {
                PreMigrationFixups.Run(db, NullLogger.Instance);
                db.Database.Migrate();

                Assert.Empty(db.Database.GetPendingMigrations());
                Assert.Equal(db.Database.GetMigrations(), db.Database.GetAppliedMigrations());
                AssertTheModelCanQueryTheMigratedSchema(db);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private static void AssertTheModelCanQueryTheMigratedSchema(AppDbContext db)
    {
        Assert.Empty(db.Submissions.ToList());
        Assert.Empty(db.Games.ToList());
    }
}
