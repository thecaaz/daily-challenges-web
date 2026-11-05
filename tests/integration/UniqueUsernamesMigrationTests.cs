using DailyChallenges.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DailyChallenges.IntegrationTests;

public class UniqueUsernamesMigrationTests
{
    private const string PreviousMigration = "20261104224220_UniqueDayWinXpEvent";

    [Fact]
    public void Case_insensitive_duplicate_usernames_are_renamed_before_the_unique_index()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.GetService<IMigrator>().Migrate(PreviousMigration);

        db.Database.ExecuteSqlRaw(
            "INSERT INTO Users (Id, Username, PasswordHash, IsAdmin, TotalXp, Level, Streak) VALUES " +
            " (1, 'Alice', 'x', 1, 0, 1, 0), (2, 'alice', 'x', 0, 0, 1, 0), (3, 'ALICE ', 'x', 0, 0, 1, 0)," +
            " (4, 'bob', 'x', 0, 0, 1, 0), (5, ' carol', 'x', 0, 0, 1, 0);" +
            "INSERT INTO Games (Id, Name, ResetTime, RankingMode) VALUES (1, 'g', '00:00:00', 0);" +
            "INSERT INTO Submissions (Id, GameId, UserId, Score, Username, ScreenshotData, ScreenshotContentType, ScoringDay, CreatedAt) VALUES " +
            " (1, 1, 2, '5', 'alice', X'', '', '2026-07-01 00:00:00', '2026-07-01 10:00:00');");

        db.Database.Migrate();

        var users = db.Users.AsNoTracking().OrderBy(u => u.Id).Select(u => new { u.Id, u.Username, u.TokenVersion }).ToList();
        Assert.Equal(new[] { "Alice", "alice_2", "ALICE_3", "bob", "carol" }, users.Select(u => u.Username));
        Assert.All(users, u => Assert.Equal(0, u.TokenVersion));

        // Rows survive the table rebuild with their foreign keys intact.
        Assert.Equal(2, db.Submissions.AsNoTracking().Single().UserId);

        // Lookups and the unique index ignore case.
        Assert.Equal(1, db.Users.Single(u => u.Username == "ALICE").Id);
        var ex = Assert.Throws<SqliteException>(() => db.Database.ExecuteSqlRaw(
            "INSERT INTO Users (Username, PasswordHash, IsAdmin, TotalXp, Level, Streak, TokenVersion) VALUES ('BOB', 'x', 0, 0, 1, 0, 0);"));
        Assert.Equal(19, ex.SqliteErrorCode);
    }
}
