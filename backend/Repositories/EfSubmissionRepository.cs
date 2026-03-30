using DailyChallenges.Data;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories
{
    public class EfSubmissionRepository : ISubmissionRepository
    {
        private readonly AppDbContext _db;
        public EfSubmissionRepository(AppDbContext db) => _db = db;

        public async Task<List<Submission>> GetByGameAsync(int gameId)
        {
            return await _db.Submissions.Where(s => s.GameId == gameId).OrderByDescending(s => s.CreatedAt).AsNoTracking().ToListAsync();
        }

        public async Task<List<Submission>> GetByGamePagedAsync(int gameId, int page, int pageSize)
        {
            if (page < 1) page = 1;
            var skip = (page - 1) * pageSize;
            return await _db.Submissions.Where(s => s.GameId == gameId).OrderByDescending(s => s.CreatedAt).Skip(skip).Take(pageSize).AsNoTracking().ToListAsync();
        }

        public async Task<List<Submission>> GetTopByGameAsync(int gameId, int top)
        {
            if (top < 1) top = 10;
            return await _db.Submissions.Where(s => s.GameId == gameId).OrderByDescending(s => s.CreatedAt).Take(top).AsNoTracking().ToListAsync();
        }

        public async Task<List<Submission>> GetTopByGameByScoreValueAsync(int gameId, int top)
        {
            if (top < 1) top = 10;
            return await _db.Submissions
                .Where(s => s.GameId == gameId && s.ScoreValue.HasValue)
                .OrderByDescending(s => s.ScoreValue)
                .ThenBy(s => s.CreatedAt)
                .Take(top)
                .AsNoTracking()
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

        public async Task<(List<Submission> Items, int TotalCount, List<DateTime> AvailableDates)> GetByGameFilteredAsync(int gameId, int page, int pageSize, string? search, DateTime? scoringDay, DateTime? excludeScoringDay = null)
        {
            IQueryable<Submission> q = _db.Submissions.Where(s => s.GameId == gameId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lower = search.ToLower();
                if (int.TryParse(search, out var parsedInt))
                {
                    q = q.Where(s => (s.Username != null && s.Username.ToLower().Contains(lower)) || (s.Score != null && s.Score.ToLower().Contains(lower)) || (s.ScoreValue.HasValue && s.ScoreValue == parsedInt));
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
            var items = await q.OrderByDescending(s => s.CreatedAt).AsNoTracking().Skip(skip).Take(pageSize).ToListAsync();

            var availableDates = await _db.Submissions
                .Where(s => s.GameId == gameId)
                .AsNoTracking()
                .Select(s => s.ScoringDay)
                .Distinct()
                .OrderByDescending(d => d)
                .ToListAsync();

            return (items, total, availableDates);
        }

        public async Task<Submission?> GetWinnerForGameAndDayAsync(int gameId, DateTime scoringDay)
        {
            var target = scoringDay.Date;
            var numericSubs = _db.Submissions.Where(s => s.GameId == gameId && s.ScoringDay == target && s.ScoreValue.HasValue);
            if (!await numericSubs.AnyAsync()) return null;
            var maxScore = await numericSubs.MaxAsync(s => s.ScoreValue!.Value);
            var winner = await numericSubs.Where(s => s.ScoreValue == maxScore).OrderBy(s => s.CreatedAt).FirstOrDefaultAsync();
            return winner;
        }

        public async Task<List<Submission>> GetWinnersForGameAndDaysAsync(int gameId, List<DateTime> days)
        {
            if (days == null || days.Count == 0) return new List<Submission>();

            var winners = await _db.Submissions
                .Where(s => s.GameId == gameId && days.Contains(s.ScoringDay) && s.ScoreValue.HasValue)
                .GroupBy(s => s.ScoringDay)
                .Select(g => g.OrderByDescending(s => s.ScoreValue).ThenBy(s => s.CreatedAt).FirstOrDefault())
                .AsNoTracking()
                .ToListAsync();

            return winners.Where(w => w != null).ToList()!;
        }

        public async Task<Submission?> GetByGameAndUserAsync(int gameId, int userId)
        {
            // return the most recent submission for the user in this game
            return await _db.Submissions.Where(s => s.GameId == gameId && s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<Submission> CreateAsync(Submission submission)
        {
            _db.Submissions.Add(submission);
            await _db.SaveChangesAsync();
            return submission;
        }

        public async Task<Submission?> GetByIdAsync(int id)
        {
            return await _db.Submissions.FirstOrDefaultAsync(s => s.Id == id);
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
    }
}
