using DailyChallenges.Data;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public sealed class EfFavoriteRepository(AppDbContext db) : IFavoriteRepository
{
    public Task<bool> ExistsAsync(int userId, int gameId) =>
        db.Favorites.AnyAsync(f => f.UserId == userId && f.GameId == gameId);

    /// <summary>
    /// Adds the favorite; adding one that already exists is a no-op. A concurrent request (double click)
    /// that inserted it first fails the unique index, which also means the favorite exists, as asked.
    /// </summary>
    public async Task AddAsync(Favorite favorite)
    {
        if (await ExistsAsync(favorite.UserId, favorite.GameId)) return;
        await db.TryInsertAsync(favorite);
    }

    public async Task RemoveAsync(int userId, int gameId)
    {
        var existing = await db.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.GameId == gameId);
        if (existing == null) return;

        db.Favorites.Remove(existing);
        await db.SaveChangesAsync();
    }

    public Task<List<int>> GetFavoriteGameIdsForUserAsync(int userId) =>
        db.Favorites
            .Where(f => f.UserId == userId)
            .Select(f => f.GameId)
            .ToListAsync();
}
