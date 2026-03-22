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
            return await _db.Games.AsNoTracking().ToListAsync();
        }

        public async Task<Game?> GetByIdAsync(int id)
        {
            return await _db.Games.Include(g => g.Submissions).FirstOrDefaultAsync(g => g.Id == id);
        }

        public async Task<Game> CreateAsync(Game game)
        {
            _db.Games.Add(game);
            await _db.SaveChangesAsync();
            return game;
        }
    }
}
