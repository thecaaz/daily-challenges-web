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

        public async Task<Submission> CreateAsync(Submission submission)
        {
            _db.Submissions.Add(submission);
            await _db.SaveChangesAsync();
            return submission;
        }
    }
}
