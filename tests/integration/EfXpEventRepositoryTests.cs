using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories;

namespace DailyChallenges.IntegrationTests;

public class EfXpEventRepositoryTests
{
    private static readonly DateTime Day1 = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = new(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc);

    private static void SeedUser(AppDbContext ctx, int id)
    {
        ctx.Users.Add(new User { Id = id, Username = $"user{id}" });
        ctx.SaveChanges();
    }

    private static void SeedEvent(AppDbContext ctx, int userId, string type, int amount, DateTime? scoringDay = null, int? gameId = null)
    {
        ctx.XpEvents.Add(new XpEvent { UserId = userId, EventType = type, Amount = amount, ScoringDay = scoringDay, GameId = gameId });
        ctx.SaveChanges();
    }

    private static void SeedGames(AppDbContext ctx, params int[] ids)
    {
        foreach (var id in ids) ctx.Games.Add(new Game { Id = id, Name = $"Game{id}" });
        ctx.SaveChanges();
    }

    private static readonly string[] TodayTypes = { "submission", "day_win" };

    // ── SumAmountByUserAndGameDaysAsync ───────────────────────────────────────

    [Fact]
    public async Task Sum_NoGames_ReturnsZero()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        var repo = new EfXpEventRepository(db.Ctx);

        var result = await repo.SumAmountByUserAndGameDaysAsync(1, new Dictionary<int, DateTime>(), TodayTypes);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Sum_MatchingGameDayAndType_SumsCorrectly()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedGames(db.Ctx, 1);
        SeedEvent(db.Ctx, 1, "submission", 50, Day1, gameId: 1);
        SeedEvent(db.Ctx, 1, "day_win", 100, Day1, gameId: 1);
        var repo = new EfXpEventRepository(db.Ctx);

        var result = await repo.SumAmountByUserAndGameDaysAsync(1, new Dictionary<int, DateTime> { [1] = Day1 }, TodayTypes);

        Assert.Equal(150, result);
    }

    [Fact]
    public async Task Sum_ExcludesOtherEventTypes()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedGames(db.Ctx, 1);
        SeedEvent(db.Ctx, 1, "submission", 50, Day1, gameId: 1);
        SeedEvent(db.Ctx, 1, "submission_revoked", -50, Day1, gameId: 1);
        var repo = new EfXpEventRepository(db.Ctx);

        var result = await repo.SumAmountByUserAndGameDaysAsync(1, new Dictionary<int, DateTime> { [1] = Day1 }, new[] { "submission" });

        Assert.Equal(50, result);
    }

    [Fact]
    public async Task Sum_ExcludesOtherUser()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedUser(db.Ctx, 2);
        SeedGames(db.Ctx, 1);
        SeedEvent(db.Ctx, 1, "submission", 50, Day1, gameId: 1);
        SeedEvent(db.Ctx, 2, "submission", 999, Day1, gameId: 1);
        var repo = new EfXpEventRepository(db.Ctx);

        var result = await repo.SumAmountByUserAndGameDaysAsync(1, new Dictionary<int, DateTime> { [1] = Day1 }, TodayTypes);

        Assert.Equal(50, result);
    }

    [Fact]
    public async Task Sum_CountsEachEventOnlyOnItsOwnGamesDay()
    {
        // Game 1's current day is Day1, game 2's is Day2 (different reset times). An event of game 1 on
        // Day2 is not "today" for game 1, even though Day2 is some other game's current day.
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedGames(db.Ctx, 1, 2);
        SeedEvent(db.Ctx, 1, "submission", 50, Day1, gameId: 1);
        SeedEvent(db.Ctx, 1, "submission", 7, Day2, gameId: 1);
        SeedEvent(db.Ctx, 1, "submission", 60, Day2, gameId: 2);
        SeedEvent(db.Ctx, 1, "day_win", 9, Day1, gameId: 2);
        var repo = new EfXpEventRepository(db.Ctx);

        var result = await repo.SumAmountByUserAndGameDaysAsync(1, new Dictionary<int, DateTime> { [1] = Day1, [2] = Day2 }, TodayTypes);

        Assert.Equal(110, result);
    }

    [Fact]
    public async Task Sum_EventWithoutGameOrDay_NotIncluded()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedGames(db.Ctx, 1);
        SeedEvent(db.Ctx, 1, "admin_adjustment", 200, null);
        SeedEvent(db.Ctx, 1, "submission", 30, Day1, gameId: null);
        var repo = new EfXpEventRepository(db.Ctx);

        var result = await repo.SumAmountByUserAndGameDaysAsync(1, new Dictionary<int, DateTime> { [1] = Day1 }, new[] { "admin_adjustment", "submission" });

        Assert.Equal(0, result);
    }

    // ── HasDayWinAsync / unique day-win index ─────────────────────────────────

    [Fact]
    public async Task HasDayWin_TrueOnlyForThatUserGameAndDay()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedGames(db.Ctx, 1, 2);
        SeedEvent(db.Ctx, 1, "day_win", 100, Day1, gameId: 1);
        var repo = new EfXpEventRepository(db.Ctx);

        Assert.True(await repo.HasDayWinAsync(1, 1, Day1));
        Assert.False(await repo.HasDayWinAsync(1, 1, Day2));
        Assert.False(await repo.HasDayWinAsync(1, 2, Day1));
    }

    [Fact]
    public async Task DuplicateDayWin_IsRejectedByTheUniqueIndex()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedGames(db.Ctx, 1);
        SeedEvent(db.Ctx, 1, "day_win", 100, Day1, gameId: 1);
        // Other event types on the same key are fine.
        SeedEvent(db.Ctx, 1, "submission", 50, Day1, gameId: 1);
        SeedEvent(db.Ctx, 1, "submission", 50, Day1, gameId: 1);

        db.Ctx.XpEvents.Add(new XpEvent { UserId = 1, GameId = 1, ScoringDay = Day1, EventType = "day_win", Amount = 100 });
        var ex = await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => db.Ctx.SaveChangesAsync());
        Assert.True(ex.IsUniqueViolation());
    }

    // ── AddAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_PersistsAndReturnsWithId()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        var repo = new EfXpEventRepository(db.Ctx);

        var ev = new XpEvent { UserId = 1, EventType = "submission", Amount = 50, ScoringDay = Day1 };
        var result = await repo.AddAsync(ev);

        Assert.True(result.Id > 0);
        Assert.Equal(50, result.Amount);
        Assert.Equal(1, db.Ctx.XpEvents.Count());
    }

    // ── GetByUserPagedAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetByUserPaged_FiltersToUserOnly()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedUser(db.Ctx, 2);
        SeedEvent(db.Ctx, 1, "submission", 50, Day1);
        SeedEvent(db.Ctx, 2, "submission", 100, Day1);
        var repo = new EfXpEventRepository(db.Ctx);

        var (items, total) = await repo.GetByUserPagedAsync(1, 1, 10);

        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.All(items, e => Assert.Equal(1, e.UserId));
    }

    [Fact]
    public async Task GetByUserPaged_FiltersByEventType()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedEvent(db.Ctx, 1, "submission", 50, Day1);
        SeedEvent(db.Ctx, 1, "day_win", 100, Day1);
        var repo = new EfXpEventRepository(db.Ctx);

        var (items, total) = await repo.GetByUserPagedAsync(1, 1, 10, eventType: "submission");

        Assert.Equal(1, total);
        Assert.All(items, e => Assert.Equal("submission", e.EventType));
    }

    [Fact]
    public async Task GetByUserPaged_FromIsInclusive_ToIsExclusive()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        foreach (var at in new[] { Day1.AddSeconds(-1), Day1, Day1.AddHours(23), Day2 })
            db.Ctx.XpEvents.Add(new XpEvent { UserId = 1, EventType = "submission", Amount = 1, CreatedAt = at });
        db.Ctx.SaveChanges();
        var repo = new EfXpEventRepository(db.Ctx);

        // The client asks for "April 1st" as [Apr 1, Apr 2).
        var (items, total) = await repo.GetByUserPagedAsync(1, 1, 10, from: Day1, to: Day2);

        Assert.Equal(2, total);
        Assert.Equal(new[] { Day1.AddHours(23), Day1 }, items.Select(e => DateTime.SpecifyKind(e.CreatedAt, DateTimeKind.Utc)));
    }
}
