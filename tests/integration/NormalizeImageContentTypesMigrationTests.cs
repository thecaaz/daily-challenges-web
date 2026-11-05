using DailyChallenges.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DailyChallenges.IntegrationTests;

public class NormalizeImageContentTypesMigrationTests
{
    private const string PreviousMigration = "20260720183454_SupportDecimalScoreValue";

    [Fact]
    public void Content_type_ends_up_matching_whether_a_blob_exists()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.GetService<IMigrator>().Migrate(PreviousMigration);

        db.Database.ExecuteSqlRaw(
            "INSERT INTO Users (Id, Username, PasswordHash, IsAdmin, TotalXp, Level, Streak) VALUES (1, 'alice', 'x', 0, 0, 1, 0);" +
            "INSERT INTO Games (Id, Name, ResetTime, RankingMode, ScreenshotData, ScreenshotContentType) VALUES " +
            " (1, 'type, no blob', '00:00:00', 0, NULL, 'image/png')," +
            " (2, 'type, empty blob', '00:00:00', 0, x'', 'image/png')," +
            " (3, 'blob and type', '00:00:00', 0, x'FFD8FF00', 'image/jpeg')," +
            " (4, 'png blob, no type', '00:00:00', 0, x'89504E4700', NULL)," +
            " (5, 'nothing', '00:00:00', 0, NULL, NULL);" +
            "INSERT INTO Submissions (Id, GameId, UserId, Score, ScreenshotData, ScreenshotContentType, CreatedAt, ScoringDay) VALUES " +
            " (10, 3, 1, '1', x'FFD8FF00', '', '2026-07-01 10:00:00', '2026-07-01 00:00:00')," +
            " (11, 3, 1, '2', x'', 'image/png', '2026-07-02 10:00:00', '2026-07-02 00:00:00')," +
            " (12, 3, 1, '3', x'5249464600', 'image/webp', '2026-07-03 10:00:00', '2026-07-03 00:00:00');");

        db.Database.Migrate();

        var games = db.Games.OrderBy(g => g.Id).Select(g => g.ScreenshotContentType).ToList();
        Assert.Equal(new[] { null, null, "image/jpeg", "image/png", null }, games);

        var subs = db.Submissions.OrderBy(s => s.Id).Select(s => s.ScreenshotContentType).ToList();
        Assert.Equal(new[] { "image/jpeg", "", "image/webp" }, subs);
    }
}
