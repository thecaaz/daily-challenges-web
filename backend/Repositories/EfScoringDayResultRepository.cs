using DailyChallenges.Data;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public sealed class EfScoringDayResultRepository(AppDbContext db) : IScoringDayResultRepository
{
    public Task<bool> ExistsAsync(int gameId, DateTime scoringDay, CancellationToken ct)
    {
        var day = scoringDay.Date;
        return db.ScoringDayResults.AnyAsync(r => r.GameId == gameId && r.ScoringDay == day, ct);
    }

    public async Task<bool> TryCreateAsync(ScoringDayResult result, CancellationToken ct)
    {
        db.ScoringDayResults.Add(result);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            db.Entry(result).State = EntityState.Detached;
            return false;
        }
    }

    public Task<ScoringDayResult?> GetAsync(int gameId, DateTime scoringDay, CancellationToken ct)
    {
        var day = scoringDay.Date;
        return db.ScoringDayResults.FirstOrDefaultAsync(r => r.GameId == gameId && r.ScoringDay == day, ct);
    }

    public async Task DeleteAsync(ScoringDayResult result, CancellationToken ct)
    {
        db.ScoringDayResults.Remove(result);
        await db.SaveChangesAsync(ct);
    }

    public Task<int> CountWinsByUserAsync(int userId, CancellationToken ct) =>
        db.ScoringDayResults.CountAsync(r => r.WinnerUserId == userId, ct);

    public async Task<DateTime?> GetLastFinalizedDayAsync(int gameId, CancellationToken ct)
    {
        var last = await db.ScoringDayResults
            .Where(r => r.GameId == gameId)
            .OrderByDescending(r => r.ScoringDay)
            .Select(r => (DateTime?)r.ScoringDay)
            .FirstOrDefaultAsync(ct);
        return last?.Date;
    }

    public async Task<List<DateTime>> GetFinalizedDaysAsync(int gameId, DateTime from, DateTime to, CancellationToken ct)
    {
        var fromDay = from.Date;
        var toDay = to.Date;
        var days = await db.ScoringDayResults
            .Where(r => r.GameId == gameId && r.ScoringDay >= fromDay && r.ScoringDay <= toDay)
            .Select(r => r.ScoringDay)
            .ToListAsync(ct);
        return days.Select(d => d.Date).ToList();
    }
}
