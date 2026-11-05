using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Services.Ranking;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories
{
    public class EfSubmissionRepository : ISubmissionRepository
    {
        private readonly AppDbContext _db;
        public EfSubmissionRepository(AppDbContext db) => _db = db;

        public async Task<List<Submission>> GetByGameAsync(int gameId)
        {
            return await _db.Submissions.Where(s => s.GameId == gameId).OrderByDescending(s => s.CreatedAt).SelectWithoutBlob().ToListAsync();
        }

        public async Task<List<Submission>> GetByGamePagedAsync(int gameId, int page, int pageSize)
        {
            if (page < 1) page = 1;
            var skip = (page - 1) * pageSize;
            return await _db.Submissions.Where(s => s.GameId == gameId).OrderByDescending(s => s.CreatedAt).Skip(skip).Take(pageSize).SelectWithoutBlob().ToListAsync();
        }

        public async Task<List<Submission>> GetTopByGameByScoreValueAsync(int gameId, int top, IRankingStrategy strategy, DateTime? excludeScoringDay = null)
        {
            if (top < 1) top = 10;
            var q = _db.Submissions.Where(s => s.GameId == gameId && s.ScoreValue.HasValue);
            if (excludeScoringDay.HasValue)
            {
                var ex = excludeScoringDay.Value.Date;
                q = q.Where(s => s.ScoringDay != ex);
            }
            return await strategy
                .ApplyOrdering(q)
                .Take(top)
                .SelectWithoutBlob()
                .ToListAsync();
        }

        public async Task<List<Submission>> GetTopByGameByUserByScoreValueAsync(int gameId, int userId, int top, IRankingStrategy strategy)
        {
            if (top < 1) top = 10;
            return await strategy
                .ApplyOrdering(_db.Submissions.Where(s => s.GameId == gameId && s.UserId == userId && s.ScoreValue.HasValue))
                .Take(top)
                .SelectWithoutBlob()
                .ToListAsync();
        }

        public async Task<List<Submission>> GetByGameAndDayByScoreValueAsync(int gameId, DateTime scoringDay, IRankingStrategy strategy)
        {
            var target = scoringDay.Date;
            return await strategy
                .ApplyOrdering(_db.Submissions.Where(s => s.GameId == gameId && s.ScoringDay == target && s.ScoreValue.HasValue))
                .SelectWithoutBlob()
                .ToListAsync();
        }

        public async Task<List<DateTime>> GetAvailableDatesAsync(int gameId)
        {
            var dates = await _db.Submissions
                .Where(s => s.GameId == gameId)
                .AsNoTracking()
                .Select(s => s.ScoringDay)
                .Distinct()
                .OrderByDescending(d => d)
                .ToListAsync();

            return dates;
        }

        public async Task<(List<Submission> Items, int TotalCount)> GetByGameFilteredAsync(int gameId, int page, int pageSize, string? search, DateTime? scoringDay, DateTime? excludeScoringDay = null)
        {
            IQueryable<Submission> q = _db.Submissions.Where(s => s.GameId == gameId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lower = search.ToLower();
                if (int.TryParse(search, out var parsedInt))
                {
                    q = q.Where(s => (s.Username != null && s.Username.ToLower().Contains(lower)) || (s.Score != null && s.Score.ToLower().Contains(lower)) || (s.ScoreValue.HasValue && s.ScoreValue >= parsedInt - 0.01 && s.ScoreValue <= parsedInt + 0.01));
                }
                else
                {
                    q = q.Where(s => (s.Username != null && s.Username.ToLower().Contains(lower)) || (s.Score != null && s.Score.ToLower().Contains(lower)));
                }
            }

            if (scoringDay.HasValue)
            {
                var target = scoringDay.Value.Date;
                q = q.Where(s => s.ScoringDay == target);
            }

            if (excludeScoringDay.HasValue)
            {
                var ex = excludeScoringDay.Value.Date;
                q = q.Where(s => s.ScoringDay != ex);
            }

            var total = await q.CountAsync();

            if (page < 1) page = 1;
            var skip = (page - 1) * pageSize;
            var items = await q.OrderByDescending(s => s.CreatedAt).Skip(skip).Take(pageSize).SelectWithoutBlob().ToListAsync();

            return (items, total);
        }

        public async Task<Submission?> GetWinnerForGameAndDayAsync(int gameId, DateTime scoringDay, IRankingStrategy strategy)
        {
            var target = scoringDay.Date;
            var numericSubs = _db.Submissions.Where(s => s.GameId == gameId && s.ScoringDay == target && s.ScoreValue.HasValue);
            if (!await numericSubs.AnyAsync()) return null;
            var winner = await strategy.ApplyOrdering(numericSubs).SelectWithoutBlob().FirstOrDefaultAsync();
            return winner;
        }

        public async Task<List<Submission>> GetWinnersForGameAndDaysAsync(int gameId, List<DateTime> days, IRankingStrategy strategy)
        {
            if (days == null || days.Count == 0) return new List<Submission>();

            var submissions = await _db.Submissions
                .Where(s => s.GameId == gameId && days.Contains(s.ScoringDay) && s.ScoreValue.HasValue)
                .SelectWithoutBlob()
                .ToListAsync();

            var winners = submissions
                .GroupBy(s => s.ScoringDay)
                .Select(g => strategy.ApplyOrdering(g.AsQueryable()).FirstOrDefault())
                .Where(w => w != null)
                .ToList();

            return winners!;
        }

        public async Task<List<Submission>> GetByUserAndGamesAndDaysAsync(int userId, List<int> gameIds, List<DateTime> scoringDays)
        {
            if (gameIds == null || gameIds.Count == 0 || scoringDays == null || scoringDays.Count == 0)
                return new List<Submission>();

            var targetDates = scoringDays.Select(d => d.Date).ToList();

            return await _db.Submissions
                .Where(s => s.UserId == userId && gameIds.Contains(s.GameId) && targetDates.Contains(s.ScoringDay))
                .SelectWithoutBlob()
                .ToListAsync();
        }

        public async Task<Submission?> GetByGameAndUserAsync(int gameId, int userId)
        {
            // return the most recent submission for the user in this game
            return await _db.Submissions.Where(s => s.GameId == gameId && s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .SelectWithoutBlob()
                .FirstOrDefaultAsync();
        }

        public Task<bool> ExistsForUserGameDayAsync(int userId, int gameId, DateTime scoringDay)
        {
            var target = scoringDay.Date;
            return _db.Submissions.AnyAsync(s => s.UserId == userId && s.GameId == gameId && s.ScoringDay == target);
        }

        public async Task<List<string>> GetUsernamesForDayAsync(int gameId, DateTime scoringDay)
        {
            var target = scoringDay.Date;
            return await _db.Submissions
                .Where(s => s.GameId == gameId && s.ScoringDay == target)
                .AsNoTracking()
                .Select(s => s.Username ?? "Anonymous")
                .ToListAsync();
        }

        public async Task<Submission> CreateAsync(Submission submission)
        {
            _db.Submissions.Add(submission);
            await _db.SaveChangesAsync();
            return submission;
        }

        /// <summary>Tracked, full entity (including the blob). Use for write paths.</summary>
        public async Task<DateTime?> GetFirstScoringDayAsync(int gameId)
        {
            var first = await _db.Submissions
                .Where(s => s.GameId == gameId)
                .OrderBy(s => s.ScoringDay)
                .Select(s => (DateTime?)s.ScoringDay)
                .FirstOrDefaultAsync();
            return first?.Date;
        }

        public async Task<Submission?> GetByIdAsync(int id)
        {
            return await _db.Submissions.FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<Submission?> GetByIdWithoutBlobAsync(int id)
        {
            return await _db.Submissions.Where(s => s.Id == id).SelectWithoutBlob().FirstOrDefaultAsync();
        }

        public async Task<(byte[]? Data, string? ContentType)> GetBlobAsync(int id)
        {
            var row = await _db.Submissions
                .Where(s => s.Id == id)
                .Select(s => new { s.ScreenshotData, s.ScreenshotContentType })
                .FirstOrDefaultAsync();
            return row == null ? (null, null) : (row.ScreenshotData, row.ScreenshotContentType);
        }

        public async Task<Submission> UpdateAsync(Submission submission)
        {
            var existing = await _db.Submissions.FirstOrDefaultAsync(s => s.Id == submission.Id);
            if (existing == null) throw new KeyNotFoundException("Submission not found");
            existing.Score = submission.Score;
            existing.ScoreValue = submission.ScoreValue;
            // Don't update screenshot here
            await _db.SaveChangesAsync();
            return existing;
        }

        public async Task DeleteAsync(int id)
        {
            var existing = await _db.Submissions.FindAsync(id);
            if (existing == null) return;
            _db.Submissions.Remove(existing);
            await _db.SaveChangesAsync();
        }

        public async Task<List<Submission>> GetByScoringDaysAsync(List<DateTime> scoringDays)
        {
            if (scoringDays == null || scoringDays.Count == 0) return new List<Submission>();
            var days = scoringDays.Select(d => d.Date).ToList();
            return await _db.Submissions
                .Where(s => days.Contains(s.ScoringDay))
                .SelectWithoutBlob()
                .ToListAsync();
        }

        public async Task<(int ActiveGameCount, int SubmissionCount)> CountOnGameDaysAsync(IReadOnlyDictionary<int, DateTime> dayByGame)
        {
            if (dayByGame == null || dayByGame.Count == 0) return (0, 0);

            // Count per (game, day) in SQL for these games and days, then keep the rows whose day is that game's
            // own day (EF can't translate a Contains over tuples). At most games x distinct-days rows come back.
            var gameIds = dayByGame.Keys.ToList();
            var days = dayByGame.Values.Select(d => d.Date).Distinct().ToList();
            var counts = await _db.Submissions
                .Where(s => gameIds.Contains(s.GameId) && days.Contains(s.ScoringDay))
                .GroupBy(s => new { s.GameId, s.ScoringDay })
                .Select(g => new { g.Key.GameId, g.Key.ScoringDay, Count = g.Count() })
                .ToListAsync();

            var matching = counts
                .Where(c => c.ScoringDay.Date == dayByGame[c.GameId].Date)
                .ToList();
            return (matching.Select(c => c.GameId).Distinct().Count(), matching.Sum(c => c.Count));
        }

        public async Task<List<Submission>> GetRecentActivityByUsersAsync(IReadOnlyCollection<int> userIds, DateTime since, int maxGames, int perGame)
        {
            var ids = userIds.ToList();
            if (ids.Count == 0 || maxGames < 1 || perGame < 1) return new List<Submission>();

            // 1) The games with the most recent activity of these users (grouped and limited in SQL).
            var recentGames = await _db.Submissions
                .Where(s => s.UserId.HasValue && ids.Contains(s.UserId.Value) && s.CreatedAt >= since)
                .GroupBy(s => s.GameId)
                .Select(g => new { GameId = g.Key, LastAt = g.Max(s => s.CreatedAt) })
                .OrderByDescending(g => g.LastAt)
                .ThenBy(g => g.GameId)
                .Take(maxGames)
                .ToListAsync();
            if (recentGames.Count == 0) return new List<Submission>();
            var gameIds = recentGames.Select(g => g.GameId).ToList();

            // 2) The latest perGame submissions of each of those games (a per-game window in SQL).
            var rows = await _db.Games
                .Where(g => gameIds.Contains(g.Id))
                .SelectMany(g => _db.Submissions
                    .Where(s => s.GameId == g.Id && s.UserId.HasValue && ids.Contains(s.UserId.Value) && s.CreatedAt >= since)
                    .OrderByDescending(s => s.CreatedAt)
                    .ThenByDescending(s => s.Id)
                    .Take(perGame))
                .SelectWithoutBlob()
                .ToListAsync();

            var gameOrder = gameIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
            return rows
                .OrderBy(s => gameOrder[s.GameId])
                .ThenByDescending(s => s.CreatedAt)
                .ThenByDescending(s => s.Id)
                .ToList();
        }

        public async Task<List<(int Id, string Score, double? ScoreValue)>> GetScoreRowsAsync()
        {
            var rows = await _db.Submissions
                .AsNoTracking()
                .OrderBy(s => s.Id)
                .Select(s => new { s.Id, s.Score, s.ScoreValue })
                .ToListAsync();
            return rows.Select(r => (r.Id, r.Score, r.ScoreValue)).ToList();
        }

        public Task<int> SetScoreValueAsync(int id, double? scoreValue) =>
            _db.Submissions.Where(s => s.Id == id).ExecuteUpdateAsync(u => u.SetProperty(s => s.ScoreValue, scoreValue));

        public Task<int> CountByUserAsync(int userId) =>
            _db.Submissions.CountAsync(s => s.UserId == userId);
    }
}
