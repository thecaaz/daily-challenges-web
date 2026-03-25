using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public async Task<(List<Submission> Items, int TotalCount, List<DateTime> AvailableDates)> GetByGameFilteredAsync(int gameId, int page, int pageSize, string? search, DateTime? scoringDay)
        {
            var game = await _db.Games.FindAsync(gameId);
            var resetTime = game?.ResetTime ?? TimeSpan.Zero;
            var resetTz = game?.ResetTimezoneId ?? "UTC";

            IQueryable<Submission> q = _db.Submissions.Where(s => s.GameId == gameId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lower = search.ToLower();
                q = q.Where(s => (s.Username != null && s.Username.ToLower().Contains(lower)) || (s.Score != null && s.Score.ToLower().Contains(lower)));
            }

            var all = await q.OrderByDescending(s => s.CreatedAt).AsNoTracking().ToListAsync();

            var scored = all.Select(s => new { Submission = s, Day = Services.ScoringDayHelper.GetScoringDay(s.CreatedAt, resetTime, resetTz) }).ToList();

            if (scoringDay.HasValue)
            {
                var target = scoringDay.Value.Date;
                scored = scored.Where(x => x.Day == target).ToList();
            }

            var availableDates = scored.Select(x => x.Day).Distinct().OrderByDescending(d => d).ToList();
            var total = scored.Count;

            if (page < 1) page = 1;
            var skip = (page - 1) * pageSize;
            var items = scored.Skip(skip).Take(pageSize).Select(x => x.Submission).ToList();

            return (items, total, availableDates);
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
