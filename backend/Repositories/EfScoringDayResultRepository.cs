using DailyChallenges.Data;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public class EfScoringDayResultRepository : IScoringDayResultRepository
{
    private readonly AppDbContext _db;
    public EfScoringDayResultRepository(AppDbContext db) => _db = db;

    public async Task<bool> ExistsAsync(int gameId, DateTime scoringDay)
    {
        var day = scoringDay.Date;
        return await _db.ScoringDayResults.AnyAsync(r => r.GameId == gameId && r.ScoringDay == day);
    }

    public async Task<bool> TryCreateAsync(ScoringDayResult result)
    {
        _db.ScoringDayResults.Add(result);
        try
        {
            await _db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            _db.Entry(result).State = EntityState.Detached;
            return false;
        }
    }

    public Task<ScoringDayResult?> GetAsync(int gameId, DateTime scoringDay)
    {
        var day = scoringDay.Date;
        return _db.ScoringDayResults.FirstOrDefaultAsync(r => r.GameId == gameId && r.ScoringDay == day);
    }

    public async Task DeleteAsync(ScoringDayResult result)
    {
        _db.ScoringDayResults.Remove(result);
        await _db.SaveChangesAsync();
    }

    public Task<int> CountWinsByUserAsync(int userId) =>
        _db.ScoringDayResults.CountAsync(r => r.WinnerUserId == userId);

    public async Task<DateTime?> GetLastFinalizedDayAsync(int gameId)
    {
        var last = await _db.ScoringDayResults
            .Where(r => r.GameId == gameId)
            .OrderByDescending(r => r.ScoringDay)
            .Select(r => (DateTime?)r.ScoringDay)
            .FirstOrDefaultAsync();
        return last?.Date;
    }

    public async Task<List<DateTime>> GetFinalizedDaysAsync(int gameId, DateTime from, DateTime to)
    {
        var fromDay = from.Date;
        var toDay = to.Date;
        var days = await _db.ScoringDayResults
            .Where(r => r.GameId == gameId && r.ScoringDay >= fromDay && r.ScoringDay <= toDay)
            .Select(r => r.ScoringDay)
            .ToListAsync();
        return days.Select(d => d.Date).ToList();
    }
}
