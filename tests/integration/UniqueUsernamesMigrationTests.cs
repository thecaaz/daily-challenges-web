using DailyChallenges.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.IntegrationTests;

public class UniqueUsernamesMigrationTests
{
    private const string PreviousMigration = "20261104224220_UniqueDayWinXpEvent";

    /// <summary>Seeds usernames that collide ignoring case and padding (and a submission by user 2), then migrates.</summary>
    private static MigrationTestDb MigrateWithCollidingUsernames()
    {
        var db = MigrationTestDb.MigratedTo(PreviousMigration);
        db.ExecuteSql(
            "INSERT INTO Users (Id, Username, PasswordHash, IsAdmin, TotalXp, Level, Streak) VALUES " +
            " (1, 'Alice', 'x', 1, 0, 1, 0), (2, 'alice', 'x', 0, 0, 1, 0), (3, 'ALICE ', 'x', 0, 0, 1, 0)," +
            " (4, 'bob', 'x', 0, 0, 1, 0), (5, ' carol', 'x', 0, 0, 1, 0);" +
            "INSERT INTO Games (Id, Name, ResetTime, RankingMode) VALUES (1, 'g', '00:00:00', 0);" +
            "INSERT INTO Submissions (Id, GameId, UserId, Score, Username, ScreenshotData, ScreenshotContentType, ScoringDay, CreatedAt) VALUES " +
            " (1, 1, 2, '5', 'alice', X'', '', '2026-07-01 00:00:00', '2026-07-01 10:00:00');");
        db.MigrateToLatest();
        return db;
    }

    [Fact]
    public void Case_insensitive_duplicate_usernames_are_renamed_before_the_unique_index()
    {
        using var db = MigrateWithCollidingUsernames();

        var users = db.Context.Users.AsNoTracking().OrderBy(u => u.Id).Select(u => new { u.Id, u.Username, u.TokenVersion }).ToList();

        Assert.Equal(new[] { "Alice", "alice_2", "ALICE_3", "bob", "carol" }, users.Select(u => u.Username));
        Assert.All(users, u => Assert.Equal(0, u.TokenVersion));
    }

    [Fact]
    public void Rows_survive_the_users_table_rebuild_with_their_foreign_keys_intact()
    {
        using var db = MigrateWithCollidingUsernames();

        var submission = db.Context.Submissions.AsNoTracking().Single();

        Assert.Equal(2, submission.UserId);
    }

    [Fact]
    public void Username_lookups_ignore_case()
    {
        using var db = MigrateWithCollidingUsernames();

        var user = db.Context.Users.Single(u => u.Username == "ALICE");

        Assert.Equal(1, user.Id);
    }

    [Fact]
    public void Unique_username_index_ignores_case()
    {
        using var db = MigrateWithCollidingUsernames();

        SqliteAssert.UniqueViolation(() => db.ExecuteSql(
            "INSERT INTO Users (Username, PasswordHash, IsAdmin, TotalXp, Level, Streak, TokenVersion) VALUES ('BOB', 'x', 0, 0, 1, 0, 0);"));
    }
}
