using DailyChallenges.Models;
using DailyChallenges.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.IntegrationTests;

public class EfScoringDayResultRepositoryTests
{
    private static readonly DateTime Day = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    private static async Task<int> SeedGameAsync(TestDb db)
    {
        var game = new Game { Name = "Claimed" };
        db.Ctx.Games.Add(game);
        await db.Ctx.SaveChangesAsync();
        return game.Id;
    }

    [Fact]
    public async Task TryCreate_FirstClaim_InsertsTheRow()
    {
        await using var db = new TestDb();
        var gameId = await SeedGameAsync(db);
        var repo = new EfScoringDayResultRepository(db.Ctx);

        var claimed = await repo.TryCreateAsync(new ScoringDayResult { GameId = gameId, ScoringDay = Day }, CancellationToken.None);

        Assert.True(claimed);
        Assert.True(await repo.ExistsAsync(gameId, Day, CancellationToken.None));
    }

    [Fact]
    public async Task TryCreate_DayAlreadyFinalized_ReturnsFalseAndLeavesNothingTracked()
    {
        await using var db = new TestDb();
        var gameId = await SeedGameAsync(db);
        var repo = new EfScoringDayResultRepository(db.Ctx);
        await repo.TryCreateAsync(new ScoringDayResult { GameId = gameId, ScoringDay = Day, WinnerUserId = null }, CancellationToken.None);
        db.Ctx.ChangeTracker.Clear();

        var duplicate = new ScoringDayResult { GameId = gameId, ScoringDay = Day };
        var claimed = await repo.TryCreateAsync(duplicate, CancellationToken.None);

        Assert.False(claimed);
        Assert.Equal(EntityState.Detached, db.Ctx.Entry(duplicate).State);
        Assert.Equal(1, await db.Ctx.ScoringDayResults.CountAsync(r => r.GameId == gameId));
    }
}
