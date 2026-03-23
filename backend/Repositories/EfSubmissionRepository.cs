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

        public async Task<Submission?> GetByGameAndUserAsync(int gameId, int userId)
        {
            return await _db.Submissions.FirstOrDefaultAsync(s => s.GameId == gameId && s.UserId == userId);
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
