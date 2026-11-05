using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services.Ranking;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.IntegrationTests;

/// <summary>List/read queries must not load image blobs; blob endpoints and write paths still must.</summary>
public class BlobFreeQueryTests
{
    private static readonly DateTime Day1 = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 };

    private static (Game Game, Submission Sub) Seed(AppDbContext ctx)
    {
        var user = new User { Id = 1, Username = "user1" };
        var game = new Game { Id = 1, Name = "G", ScreenshotData = Png, ScreenshotContentType = "image/png" };
        ctx.Users.Add(user);
        ctx.Games.Add(game);
        var sub = new Submission
        {
            GameId = 1, UserId = 1, Username = "user1", Score = "10", ScoreValue = 10, ScoringDay = Day1,
            CreatedAt = Day1.AddHours(1), XpAwarded = 50, ScreenshotData = Png, ScreenshotContentType = "image/png"
        };
        ctx.Submissions.Add(sub);
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();
        return (game, sub);
    }

    [Fact]
    public async Task Submission_list_methods_return_every_column_except_the_blob()
    {
        await using var db = new TestDb();
        var (_, seeded) = Seed(db.Ctx);
        var repo = new EfSubmissionRepository(db.Ctx);
        var strategy = new HighestRankingStrategy();

        var results = new List<Submission?>
        {
            (await repo.GetByGameAsync(1)).Single(),
            (await repo.GetByGamePagedAsync(1, 1, 10)).Single(),
            (await repo.GetTopByGameByScoreValueAsync(1, 10, strategy)).Single(),
            (await repo.GetTopByGameByUserByScoreValueAsync(1, 1, 10, strategy)).Single(),
            (await repo.GetByGameAndDayByScoreValueAsync(1, Day1, strategy)).Single(),
            await repo.GetByGameAndUserAsync(1, 1),
            (await repo.GetByUserAndGamesAndDaysAsync(1, new List<int> { 1 }, new List<DateTime> { Day1 })).Single(),
            (await repo.GetByScoringDaysAsync(new List<DateTime> { Day1 })).Single(),
            (await repo.GetRecentActivityByUsersAsync(new[] { 1 }, Day1, 10, 5)).Single(),
            (await repo.GetByGameFilteredAsync(1, 1, 10, null, null)).Items.Single(),
            await repo.GetWinnerForGameAndDayAsync(1, Day1, strategy),
            (await repo.GetWinnersForGameAndDaysAsync(1, new List<DateTime> { Day1 }, strategy)).Single(),
            await repo.GetByIdWithoutBlobAsync(seeded.Id),
        };

        Assert.All(results, s =>
        {
            Assert.NotNull(s);
            Assert.Empty(s!.ScreenshotData);
            Assert.Equal("image/png", s.ScreenshotContentType);
            Assert.Equal(seeded.Id, s.Id);
            Assert.Equal(1, s.GameId);
            Assert.Equal(1, s.UserId);
            Assert.Equal("user1", s.Username);
            Assert.Equal("10", s.Score);
            Assert.Equal(10.0, s.ScoreValue);
            Assert.Equal(Day1, s.ScoringDay);
            Assert.Equal(Day1.AddHours(1), s.CreatedAt);
            Assert.Equal(50, s.XpAwarded);
        });
        Assert.Empty(db.Ctx.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Submission_blob_and_write_paths_still_see_the_blob()
    {
        await using var db = new TestDb();
        var (_, seeded) = Seed(db.Ctx);
        var repo = new EfSubmissionRepository(db.Ctx);

        var (data, contentType) = await repo.GetBlobAsync(seeded.Id);
        Assert.Equal(Png, data);
        Assert.Equal("image/png", contentType);
        Assert.Equal((null, null), await repo.GetBlobAsync(9999));

        // Update keeps the blob, delete removes the row.
        var tracked = await repo.GetByIdAsync(seeded.Id);
        Assert.Equal(Png, tracked!.ScreenshotData);
        tracked.Score = "11";
        await repo.UpdateAsync(tracked);
        db.Ctx.ChangeTracker.Clear();
        Assert.Equal(Png, (await repo.GetBlobAsync(seeded.Id)).Data);

        await repo.DeleteAsync(seeded.Id);
        Assert.Null(await repo.GetByIdAsync(seeded.Id));
    }

    [Fact]
    public async Task Game_list_omits_blob_but_blob_and_write_paths_work()
    {
        await using var db = new TestDb();
        Seed(db.Ctx);
        var repo = new EfGameRepository(db.Ctx);

        var listed = (await repo.GetAllAsync()).Single();
        Assert.Null(listed.ScreenshotData);
        Assert.Equal("image/png", listed.ScreenshotContentType);
        Assert.Equal("G", listed.Name);

        Assert.Equal(Png, (await repo.GetBlobAsync(1)).Data);

        var tracked = await repo.GetByIdAsync(1);
        tracked!.Name = "Renamed";
        await repo.UpdateAsync(tracked);
        db.Ctx.ChangeTracker.Clear();
        Assert.Equal("Renamed", (await repo.GetAllAsync()).Single().Name);
        Assert.Equal(Png, (await repo.GetBlobAsync(1)).Data);
    }
}

/// <summary>DbUpdateExceptionExtensions.IsUniqueViolation against real SQLite errors.</summary>
public class DbUpdateExceptionExtensionsTests
{
    [Fact]
    public async Task Unique_index_violation_is_detected()
    {
        await using var db = new TestDb();
        db.Ctx.Users.Add(new User { Id = 1, Username = "u" });
        db.Ctx.Games.Add(new Game { Id = 1, Name = "G" });
        db.Ctx.SaveChanges();
        db.Ctx.Favorites.Add(new Favorite { UserId = 1, GameId = 1 });
        db.Ctx.SaveChanges();

        db.Ctx.Favorites.Add(new Favorite { UserId = 1, GameId = 1 });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.Ctx.SaveChangesAsync());

        Assert.True(ex.IsUniqueViolation());
    }

    [Fact]
    public async Task Primary_key_violation_is_detected()
    {
        await using var db = new TestDb();
        db.Ctx.Games.Add(new Game { Id = 1, Name = "G" });
        db.Ctx.SaveChanges();
        db.Ctx.ChangeTracker.Clear();

        db.Ctx.Games.Add(new Game { Id = 1, Name = "Again" });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.Ctx.SaveChangesAsync());

        Assert.True(ex.IsUniqueViolation());
    }

    [Fact]
    public async Task Other_constraint_violations_are_not_unique_violations()
    {
        await using var db = new TestDb();
        db.Ctx.Games.Add(new Game { Id = 1, Name = null! }); // NOT NULL constraint
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.Ctx.SaveChangesAsync());

        var sqlite = Assert.IsType<SqliteException>(ex.InnerException);
        Assert.Equal(19, sqlite.SqliteErrorCode);
        Assert.False(ex.IsUniqueViolation());
    }
}
