using DailyChallenges.Data;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories
{
    public class EfFavoriteRepository : IFavoriteRepository
    {
        private readonly AppDbContext _db;
        public EfFavoriteRepository(AppDbContext db) => _db = db;

        public async Task<bool> ExistsAsync(int userId, int gameId)
        {
            return await _db.Favorites.AnyAsync(f => f.UserId == userId && f.GameId == gameId);
        }

        public async Task AddAsync(Favorite favorite)
        {
            if (await ExistsAsync(favorite.UserId, favorite.GameId)) return;
            _db.Favorites.Add(favorite);
            await _db.SaveChangesAsync();
        }

        public async Task RemoveAsync(int userId, int gameId)
        {
            var existing = await _db.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.GameId == gameId);
            if (existing == null) return;
            _db.Favorites.Remove(existing);
            await _db.SaveChangesAsync();
        }

        public async Task<List<int>> GetFavoriteGameIdsForUserAsync(int userId)
        {
            return await _db.Favorites
                .Where(f => f.UserId == userId)
                .Select(f => f.GameId)
                .ToListAsync();
        }
    }
}
