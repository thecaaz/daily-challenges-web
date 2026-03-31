using DailyChallenges.Data;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories
{
    public class EfScoringDayResultRepository : IScoringDayResultRepository
    {
        private readonly AppDbContext _db;
        public EfScoringDayResultRepository(AppDbContext db) => _db = db;

        public async Task<bool> ExistsAsync(int gameId, DateTime scoringDay)
        {
            var day = scoringDay.Date;
            return await _db.ScoringDayResults.AnyAsync(r => r.GameId == gameId && r.ScoringDay == day);
        }

        public async Task<ScoringDayResult> CreateAsync(ScoringDayResult result)
        {
            _db.ScoringDayResults.Add(result);
            await _db.SaveChangesAsync();
            return result;
        }
    }
}
