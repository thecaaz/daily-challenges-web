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

        public async Task<User?> GetByUsernameAsync(string username)
        {
            return await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task<List<UserGameStatDto>> GetMostPlayedGamesAsync(int userId, int topGames = 10)
        {
            if (topGames < 1) topGames = 10;

            var grouped = _db.Submissions
                .Where(s => s.UserId == userId && s.ScoreValue != null)
                .GroupBy(s => s.GameId)
                .Select(g => new
                {
                    GameId = g.Key,
                    Plays = g.Count(),
                    MaxScore = g.Max(x => x.ScoreValue),
                    MinScore = g.Min(x => x.ScoreValue),
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
                        // WARNING: Will need to be adjusted if we add more ranking modes in the future
                        HighestScore = gm.RankingMode == RankingMode.Highest ? g.MaxScore : g.MinScore,
                        LastPlayed = g.LastPlayed,
                        RankingMode = gm.RankingMode == RankingMode.Highest ? "highest" : "lowest"
                    };

            var list = await q.OrderByDescending(x => x.Plays).Take(topGames).ToListAsync();

            await PopulateBestSubmissionIdAsync(userId, list);

            return list;
        }

        private async Task PopulateBestSubmissionIdAsync(int userId, List<UserGameStatDto> list)
        {
            var gameIdsWithScore = list.Where(x => x.HighestScore.HasValue).Select(x => x.GameId).ToList();

            if (gameIdsWithScore.Count <= 0) return;
            
            var scoreLookup = await _db.Submissions
                .Where(s => s.UserId == userId && gameIdsWithScore.Contains(s.GameId) && s.ScoreValue != null)
                .GroupBy(s => new { s.GameId, ScoreKey = Math.Round(s.ScoreValue!.Value, 2) })
                .Select(g => new
                {
                    g.Key.GameId,
                    ScoreKey = g.Key.ScoreKey,
                    SubmissionId = g.OrderByDescending(x => x.CreatedAt).Select(x => x.Id).FirstOrDefault()
                })
                .ToListAsync();

            var dict = scoreLookup.ToDictionary(x => (x.GameId, x.ScoreKey), x => (int?)x.SubmissionId);

            foreach (var item in list)
            {
                if (!item.HighestScore.HasValue) continue;

                dict.TryGetValue((item.GameId, Math.Round(item.HighestScore.Value, 2)), out var submissionId);
                item.BestSubmissionId = submissionId;

            }
        }

    }
}
