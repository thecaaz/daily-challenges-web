using DailyChallenges.Data;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

/// <summary>
/// Inserts that leave the context clean when they fail: a failed insert stays Added in the change tracker,
/// so without detaching it every later SaveChanges in the same scope would retry (and fail on) it.
/// </summary>
public static class InsertExtensions
{
    /// <summary>Saves the pending inserts; on failure detaches them and rethrows.</summary>
    public static async Task SaveInsertsOrDetachAsync(this AppDbContext db, IEnumerable<object> inserted)
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch
        {
            Detach(db, inserted);
            throw;
        }
    }

    /// <summary>
    /// Adds and saves <paramref name="entity"/>. Returns false, with the entity detached, when the row
    /// already exists (unique violation); any other failure is rethrown.
    /// </summary>
    public static async Task<bool> TryInsertAsync<TEntity>(this AppDbContext db, TEntity entity) where TEntity : class
    {
        db.Add(entity);
        try
        {
            await db.SaveInsertsOrDetachAsync([entity]);
            return true;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return false;
        }
    }

    private static void Detach(AppDbContext db, IEnumerable<object> entities)
    {
        foreach (var entity in entities)
            db.Entry(entity).State = EntityState.Detached;
    }
}
