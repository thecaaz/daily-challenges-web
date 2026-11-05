using DailyChallenges.Data;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories
{
    public class EfGameRepository : IGameRepository
    {
        private readonly AppDbContext _db;
        public EfGameRepository(AppDbContext db) => _db = db;

        public async Task<List<Game>> GetAllAsync()
        {
            return await _db.Games.SelectWithoutBlob().ToListAsync();
        }

        /// <summary>Tracked, full entity (including the image blob). Write paths rely on tracking.</summary>
        public async Task<Game?> GetByIdAsync(int id)
        {
            return await _db.Games.FirstOrDefaultAsync(g => g.Id == id);
        }

        public async Task<Game?> GetByIdWithoutBlobAsync(int id)
        {
            return await _db.Games.Where(g => g.Id == id).SelectWithoutBlob().FirstOrDefaultAsync();
        }

        public async Task<(byte[]? Data, string? ContentType)> GetBlobAsync(int id)
        {
            var row = await _db.Games
                .Where(g => g.Id == id)
                .Select(g => new { g.ScreenshotData, g.ScreenshotContentType })
                .FirstOrDefaultAsync();
            return row == null ? (null, null) : (row.ScreenshotData, row.ScreenshotContentType);
        }

        public async Task<Game> CreateAsync(Game game)
        {
            _db.Games.Add(game);
            await _db.SaveChangesAsync();
            return game;
        }

        public async Task<Game> UpdateAsync(Game game)
        {
            var existing = await _db.Games.FirstOrDefaultAsync(g => g.Id == game.Id);
            if (existing == null) throw new KeyNotFoundException("Game not found");
            existing.Name = game.Name;
            existing.Url = game.Url;
            existing.ResetTime = game.ResetTime;
            existing.Description = game.Description;
            existing.RankingMode = game.RankingMode;
            if (game.ScreenshotData != null && game.ScreenshotData.Length > 0)
            {
                existing.ScreenshotData = game.ScreenshotData;
                existing.ScreenshotContentType = game.ScreenshotContentType;
            }
            await _db.SaveChangesAsync();
            return existing;
        }

        public async Task DeleteAsync(int id)
        {
            var existing = await _db.Games.FindAsync(id);
            if (existing == null) return;
            _db.Games.Remove(existing);
            await _db.SaveChangesAsync();
        }
    }
}
