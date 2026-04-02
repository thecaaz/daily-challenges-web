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

            var q = from s in _db.Submissions
                    where s.UserId == userId
                    group s by s.GameId into g
                    join gm in _db.Games on g.Key equals gm.Id
                    select new UserGameStatDto
                    {
                        GameId = g.Key,
                        Name = gm.Name,
                        Url = gm.Url,
                        Plays = g.Count(),
                        HighestScore = g.Max(x => x.ScoreValue),
                        LastPlayed = g.Max(x => x.CreatedAt)
                    };

            var list = await q.OrderByDescending(x => x.Plays).Take(topGames).AsNoTracking().ToListAsync();
            return list;
        }
    }
}
