using DailyChallenges.Data;

namespace DailyChallenges.Repositories;

public sealed class EfTransactionRunner(AppDbContext db) : ITransactionRunner
{
    public async Task<T> RunAsync<T>(Func<Task<T>> work)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var result = await work();
            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            db.ChangeTracker.Clear();
            throw;
        }
    }
}
