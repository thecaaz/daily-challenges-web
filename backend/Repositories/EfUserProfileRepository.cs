using DailyChallenges.Data;
using DailyChallenges.DTOs;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories
{
    public class EfUserProfileRepository : IUserProfileRepository
    {
        private readonly AppDbContext _db;
        public EfUserProfileRepository(AppDbContext db) => _db = db;

        public async Task<User?> GetByIdAsync(int userId)
        {
            return await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        }

        public async Task<List<UserGameStatDto>> GetMostPlayedGamesAsync(int userId, int topGames = 10)
        {
            if (topGames < 1) topGames = 10;

            var grouped = _db.Submissions
                .Where(s => s.UserId == userId)
                .GroupBy(s => s.GameId)
                .Select(g => new
                {
                    GameId = g.Key,
                    Plays = g.Count(),
                    HighestScore = g.Max(x => x.ScoreValue),
                    LastPlayed = g.Max(x => x.CreatedAt)
                });

            var q = from g in grouped
                    join gm in _db.Games.AsNoTracking() on g.GameId equals gm.Id
                    select new UserGameStatDto
                    {
                        GameId = g.GameId,
                        Name = gm.Name,
                        Url = gm.Url,
                        Plays = g.Plays,
                        HighestScore = g.HighestScore,
                        LastPlayed = g.LastPlayed
                    };

            var list = await q.OrderByDescending(x => x.Plays).Take(topGames).ToListAsync();
            return list;
        }
    }
}
