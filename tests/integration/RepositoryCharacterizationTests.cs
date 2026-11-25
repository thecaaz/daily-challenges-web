using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services.Ranking;

namespace DailyChallenges.IntegrationTests;

/// <summary>Pins paging, filtering and default behaviour of the EF repositories.</summary>
public class RepositoryCharacterizationTests
{
    private static readonly DateTime Day1 = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = new(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc);

    private static void SeedGame(AppDbContext ctx, int id)
    {
        ctx.Games.Add(new Game { Id = id, Name = $"Game{id}" });
        ctx.SaveChanges();
    }

    private static Submission SeedSub(AppDbContext ctx, int gameId, double? scoreValue, DateTime scoringDay, string? username, int minute)
    {
        var sub = new Submission
        {
            GameId = gameId,
            Score = scoreValue?.ToString() ?? "x",
            ScoreValue = scoreValue,
            ScoringDay = scoringDay.Date,
            Username = username,
            CreatedAt = scoringDay.AddMinutes(minute)
        };
        ctx.Submissions.Add(sub);
        ctx.SaveChanges();
        return sub;
    }

    [Fact]
    public async Task Submissions_filtered_search_matches_username_score_text_and_near_numbers()
    {
        await using var db = new TestDb();
        SeedGame(db.Ctx, 1);
        var alice = SeedSub(db.Ctx, 1, 41.995, Day1, "Alice", 1);
        var bob = SeedSub(db.Ctx, 1, 7, Day1, "bob", 2);
        var carol = SeedSub(db.Ctx, 1, 42.5, Day1, "carol", 3);
        var repo = new EfSubmissionRepository(db.Ctx);

        var byName = await repo.GetByGameFilteredAsync(1, 1, 10, "ALI", null);
        var byNumber = await repo.GetByGameFilteredAsync(1, 1, 10, "42", null);
        var byScoreText = await repo.GetByGameFilteredAsync(1, 1, 10, "7", null);

        Assert.Equal(new[] { alice.Id }, byName.Items.Select(s => s.Id));
        Assert.Equal(new[] { carol.Id, alice.Id }, byNumber.Items.Select(s => s.Id));
        Assert.Equal(new[] { bob.Id }, byScoreText.Items.Select(s => s.Id));
    }

    [Fact]
    public async Task Submissions_filtered_pages_newest_first_and_filters_days()
    {
        await using var db = new TestDb();
        SeedGame(db.Ctx, 1);
        var first = SeedSub(db.Ctx, 1, 1, Day1, "a", 1);
        var second = SeedSub(db.Ctx, 1, 2, Day1, "b", 2);
        var third = SeedSub(db.Ctx, 1, 3, Day2, "c", 3);
        var repo = new EfSubmissionRepository(db.Ctx);

        var pageZero = await repo.GetByGameFilteredAsync(1, 0, 2, null, null);
        var pageTwo = await repo.GetByGameFilteredAsync(1, 2, 2, null, null);
        var onDay1 = await repo.GetByGameFilteredAsync(1, 1, 10, null, Day1.AddHours(5));
        var withoutDay2 = await repo.GetByGameFilteredAsync(1, 1, 10, "  ", null, Day2.AddHours(5));

        Assert.Equal(3, pageZero.TotalCount);
        Assert.Equal(new[] { third.Id, second.Id }, pageZero.Items.Select(s => s.Id));
        Assert.Equal(new[] { first.Id }, pageTwo.Items.Select(s => s.Id));
        Assert.Equal(2, onDay1.TotalCount);
        Assert.Equal(new[] { second.Id, first.Id }, withoutDay2.Items.Select(s => s.Id));
    }

    [Fact]
    public async Task Submissions_paged_treats_page_below_one_as_first_page()
    {
        await using var db = new TestDb();
        SeedGame(db.Ctx, 1);
        SeedSub(db.Ctx, 1, 1, Day1, "a", 1);
        var newest = SeedSub(db.Ctx, 1, 2, Day1, "b", 2);
        var repo = new EfSubmissionRepository(db.Ctx);

        var page = await repo.GetByGamePagedAsync(1, 0, 1);

        Assert.Equal(newest.Id, Assert.Single(page).Id);
    }

    [Fact]
    public async Task Submissions_top_scores_default_to_ten_and_can_exclude_a_day()
    {
        await using var db = new TestDb();
        SeedGame(db.Ctx, 1);
        for (var i = 0; i < 12; i++) SeedSub(db.Ctx, 1, i, Day1, "a", i);
        var best = SeedSub(db.Ctx, 1, 100, Day2, "b", 0);
        SeedSub(db.Ctx, 1, null, Day1, "c", 30);
        var repo = new EfSubmissionRepository(db.Ctx);
        var strategy = new HighestRankingStrategy();

        var defaulted = await repo.GetTopByGameByScoreValueAsync(1, 0, strategy);
        var withoutDay2 = await repo.GetTopByGameByScoreValueAsync(1, 3, strategy, Day2.AddHours(1));

        Assert.Equal(10, defaulted.Count);
        Assert.Equal(best.Id, defaulted[0].Id);
        Assert.Equal(new double?[] { 11, 10, 9 }, withoutDay2.Select(s => s.ScoreValue));
    }

    [Fact]
    public async Task Submissions_latest_for_user_and_usernames_for_day()
    {
        await using var db = new TestDb();
        SeedGame(db.Ctx, 1);
        db.Ctx.Users.Add(new User { Id = 4, Username = "dora" });
        db.Ctx.SaveChanges();
        var older = SeedSub(db.Ctx, 1, 1, Day1, "dora", 1);
        older.UserId = 4;
        var newer = SeedSub(db.Ctx, 1, 2, Day2, "dora", 2);
        newer.UserId = 4;
        SeedSub(db.Ctx, 1, 3, Day1, null, 3);
        db.Ctx.SaveChanges();
        var repo = new EfSubmissionRepository(db.Ctx);

        var latest = await repo.GetByGameAndUserAsync(1, 4);
        var usernames = await repo.GetUsernamesForDayAsync(1, Day1.AddHours(3));

        Assert.Equal(newer.Id, latest!.Id);
        Assert.Equal(new[] { "Anonymous", "dora" }, usernames.OrderBy(n => n));
    }

    [Fact]
    public async Task Submissions_update_changes_only_the_score()
    {
        await using var db = new TestDb();
        SeedGame(db.Ctx, 1);
        var sub = SeedSub(db.Ctx, 1, 1, Day1, "a", 1);
        var repo = new EfSubmissionRepository(db.Ctx);

        var updated = await repo.UpdateAsync(new Submission { Id = sub.Id, Score = "9", ScoreValue = 9, Username = "other" });

        Assert.Equal("9", updated.Score);
        Assert.Equal(9, updated.ScoreValue);
        Assert.Equal("a", updated.Username);
        var missing = await Assert.ThrowsAsync<KeyNotFoundException>(() => repo.UpdateAsync(new Submission { Id = 999 }));
        Assert.Equal("Submission not found", missing.Message);
    }

    [Fact]
    public async Task Notifications_page_defaults_and_counts_unread()
    {
        await using var db = new TestDb();
        db.Ctx.Users.Add(new User { Id = 1, Username = "u" });
        for (var i = 0; i < 25; i++)
            db.Ctx.Notifications.Add(new Notification { UserId = 1, Message = $"m{i}", Type = "t", ScoringDay = Day1, IsRead = i % 2 == 0, CreatedAt = Day1.AddMinutes(i) });
        db.Ctx.SaveChanges();
        var repo = new EfNotificationRepository(db.Ctx);

        var (items, total, unread) = await repo.GetByUserPagedAsync(1, 0, 0);

        Assert.Equal(20, items.Count);
        Assert.Equal("m24", items[0].Message);
        Assert.Equal(25, total);
        Assert.Equal(12, unread);
    }

    [Fact]
    public async Task Notifications_delete_day_results_only_removes_win_and_placement()
    {
        await using var db = new TestDb();
        db.Ctx.Users.Add(new User { Id = 1, Username = "u" });
        SeedGame(db.Ctx, 1);
        foreach (var type in new[] { "day_win", "day_placement", "friend_request" })
            db.Ctx.Notifications.Add(new Notification { UserId = 1, GameId = 1, Message = type, Type = type, ScoringDay = Day1, CreatedAt = Day1 });
        db.Ctx.SaveChanges();
        var repo = new EfNotificationRepository(db.Ctx);

        var deleted = await repo.DeleteDayResultNotificationsAsync(1, Day1.AddHours(2));

        Assert.Equal(2, deleted);
        Assert.Equal("friend_request", db.Ctx.Notifications.Single().Type);
    }

    [Fact]
    public async Task Users_page_defaults_and_searches_case_insensitively()
    {
        await using var db = new TestDb();
        for (var i = 1; i <= 55; i++) db.Ctx.Users.Add(new User { Id = i, Username = i == 7 ? "MixedCase" : $"user{i}" });
        db.Ctx.SaveChanges();
        var repo = new EfUserRepository(db.Ctx);

        var (firstPage, total) = await repo.GetPagedAsync(0, 0, null);
        var (found, foundTotal) = await repo.GetPagedAsync(1, 10, "  mixed ");

        Assert.Equal(50, firstPage.Count);
        Assert.Equal(1, firstPage[0].Id);
        Assert.Equal(55, total);
        Assert.Equal(7, Assert.Single(found).Id);
        Assert.Equal(1, foundTotal);
    }

    [Fact]
    public async Task Xp_events_page_defaults_to_twenty()
    {
        await using var db = new TestDb();
        db.Ctx.Users.Add(new User { Id = 1, Username = "u" });
        for (var i = 0; i < 25; i++)
            db.Ctx.XpEvents.Add(new XpEvent { UserId = 1, Amount = i, EventType = "admin_adjustment", CreatedAt = Day1.AddMinutes(i) });
        db.Ctx.SaveChanges();
        var repo = new EfXpEventRepository(db.Ctx);

        var (items, total) = await repo.GetByUserPagedAsync(1, 0, 0);

        Assert.Equal(20, items.Count);
        Assert.Equal(24, items[0].Amount);
        Assert.Equal(25, total);
    }

    [Fact]
    public async Task Game_update_keeps_the_image_when_none_is_sent()
    {
        await using var db = new TestDb();
        db.Ctx.Games.Add(new Game { Id = 1, Name = "Old", ScreenshotData = new byte[] { 1 }, ScreenshotContentType = "image/png" });
        db.Ctx.SaveChanges();
        var repo = new EfGameRepository(db.Ctx);

        var updated = await repo.UpdateAsync(new Game { Id = 1, Name = "New", ScreenshotData = Array.Empty<byte>() });

        Assert.Equal("New", updated.Name);
        Assert.Equal(new byte[] { 1 }, updated.ScreenshotData);
        Assert.Equal("image/png", updated.ScreenshotContentType);
        var missing = await Assert.ThrowsAsync<KeyNotFoundException>(() => repo.UpdateAsync(new Game { Id = 99 }));
        Assert.Equal("Game not found", missing.Message);
    }
}
