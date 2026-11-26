using DailyChallenges.Data;

namespace DailyChallenges.Repositories;

/// <summary>
/// The rollback uses <see cref="CancellationToken.None"/>: it must run to the end even when the work failed
/// because the request was aborted.
/// </summary>
public sealed class EfTransactionRunner(AppDbContext db) : ITransactionRunner
{
    public async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await work();
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }
}
