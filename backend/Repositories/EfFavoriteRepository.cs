using DailyChallenges.Data;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public sealed class EfFavoriteRepository(AppDbContext db) : IFavoriteRepository
{
    public Task<bool> ExistsAsync(int userId, int gameId, CancellationToken ct) =>
        db.Favorites.AnyAsync(f => f.UserId == userId && f.GameId == gameId, ct);

    /// <summary>
    /// Adds the favorite; adding one that already exists is a no-op. A concurrent request (double click)
    /// that inserted it first fails the unique index, which also means the favorite exists, as asked.
    /// </summary>
    public async Task AddAsync(Favorite favorite, CancellationToken ct)
    {
        if (await ExistsAsync(favorite.UserId, favorite.GameId, ct)) return;
        await db.TryInsertAsync(favorite, ct);
    }

    public async Task RemoveAsync(int userId, int gameId, CancellationToken ct)
    {
        var existing = await db.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.GameId == gameId, ct);
        if (existing == null) return;

        db.Favorites.Remove(existing);
        await db.SaveChangesAsync(ct);
    }

    public Task<List<int>> GetFavoriteGameIdsForUserAsync(int userId, CancellationToken ct) =>
        db.Favorites
            .Where(f => f.UserId == userId)
            .Select(f => f.GameId)
            .ToListAsync(ct);
}
