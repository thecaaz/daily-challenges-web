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

    private static void SeedEvent(AppDbContext ctx, int userId, string type, int amount, DateTime? scoringDay = null)
    {
        ctx.XpEvents.Add(new XpEvent { UserId = userId, EventType = type, Amount = amount, ScoringDay = scoringDay });
        ctx.SaveChanges();
    }

    // ── SumAmountByUserAndDaysAndTypesAsync ───────────────────────────────────

    [Fact]
    public async Task Sum_EmptyDays_ReturnsZero()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        var repo = new EfXpEventRepository(db.Ctx);

        var result = await repo.SumAmountByUserAndDaysAndTypesAsync(1, new List<DateTime>(), new[] { "submission" });

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Sum_MatchingDayAndType_SumsCorrectly()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedEvent(db.Ctx, 1, "submission", 50, Day1);
        SeedEvent(db.Ctx, 1, "submission", 60, Day1);
        var repo = new EfXpEventRepository(db.Ctx);

        var result = await repo.SumAmountByUserAndDaysAndTypesAsync(1, new List<DateTime> { Day1 }, new[] { "submission" });

        Assert.Equal(110, result);
    }

    [Fact]
    public async Task Sum_ExcludesOtherEventType()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedEvent(db.Ctx, 1, "submission", 50, Day1);
        SeedEvent(db.Ctx, 1, "day_win", 100, Day1);
        var repo = new EfXpEventRepository(db.Ctx);

        var result = await repo.SumAmountByUserAndDaysAndTypesAsync(1, new List<DateTime> { Day1 }, new[] { "submission" });

        Assert.Equal(50, result);
    }

    [Fact]
    public async Task Sum_ExcludesOtherScoringDay()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedEvent(db.Ctx, 1, "submission", 50, Day1);
        SeedEvent(db.Ctx, 1, "submission", 75, Day2);
        var repo = new EfXpEventRepository(db.Ctx);

        var result = await repo.SumAmountByUserAndDaysAndTypesAsync(1, new List<DateTime> { Day1 }, new[] { "submission" });

        Assert.Equal(50, result);
    }

    [Fact]
    public async Task Sum_ExcludesOtherUser()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedUser(db.Ctx, 2);
        SeedEvent(db.Ctx, 1, "submission", 50, Day1);
        SeedEvent(db.Ctx, 2, "submission", 999, Day1);
        var repo = new EfXpEventRepository(db.Ctx);

        var result = await repo.SumAmountByUserAndDaysAndTypesAsync(1, new List<DateTime> { Day1 }, new[] { "submission" });

        Assert.Equal(50, result);
    }

    [Fact]
    public async Task Sum_MultipleMatchingEvents_SumsAll()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedEvent(db.Ctx, 1, "submission", 50, Day1);
        SeedEvent(db.Ctx, 1, "streak_bonus", 5, Day1);
        SeedEvent(db.Ctx, 1, "submission", 60, Day2);
        var repo = new EfXpEventRepository(db.Ctx);

        var result = await repo.SumAmountByUserAndDaysAndTypesAsync(
            1, new List<DateTime> { Day1, Day2 }, new[] { "submission", "streak_bonus" });

        Assert.Equal(115, result);
    }

    [Fact]
    public async Task Sum_EventWithNullScoringDay_NotIncluded()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedEvent(db.Ctx, 1, "admin_adjustment", 200, null);
        var repo = new EfXpEventRepository(db.Ctx);

        var result = await repo.SumAmountByUserAndDaysAndTypesAsync(1, new List<DateTime> { Day1 }, new[] { "admin_adjustment" });

        Assert.Equal(0, result);
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
}
