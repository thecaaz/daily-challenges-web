using DailyChallenges.Models;
using DailyChallenges.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.IntegrationTests;

/// <summary>An aborted request cancels its transaction part-way; nothing of the work may be committed.</summary>
public class TransactionCancellationTests
{
    [Fact]
    public async Task RunAsync_rolls_back_saved_work_when_the_token_is_cancelled_part_way()
    {
        await using var db = new TestDb();
        var transactions = new EfTransactionRunner(db.Ctx);
        using var request = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transactions.RunAsync(async () =>
        {
            db.Ctx.Users.Add(new User { Username = "first" });
            await db.Ctx.SaveChangesAsync(request.Token);
            await request.CancelAsync();
            db.Ctx.Users.Add(new User { Username = "second" });
            await db.Ctx.SaveChangesAsync(request.Token);
            return true;
        }, request.Token));

        Assert.Empty(db.Ctx.ChangeTracker.Entries());
        Assert.Equal(0, await db.Ctx.Users.CountAsync());
    }
}
