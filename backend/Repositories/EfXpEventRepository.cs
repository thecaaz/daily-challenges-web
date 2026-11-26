using DailyChallenges.Data;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public sealed class EfXpEventRepository(AppDbContext db) : IXpEventRepository
{
    private const int DefaultPageSize = 20;

    /// <summary>
    /// Newest first. <see cref="XpEventFilter.From"/> is inclusive, <see cref="XpEventFilter.To"/> exclusive (the
    /// client sends the day after the last day it wants). A page size below 1 reads as <see cref="DefaultPageSize"/>.
    /// </summary>
    public async Task<(List<XpEvent> Items, int TotalCount)> GetByUserPagedAsync(XpEventFilter filter, int page, int pageSize, CancellationToken ct)
    {
        var (from, to, eventType) = (filter.From, filter.To, filter.EventType);
        IQueryable<XpEvent> query = db.XpEvents.Where(e => e.UserId == filter.UserId);
        if (from.HasValue) query = query.Where(e => e.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(e => e.CreatedAt < to.Value);
        if (!string.IsNullOrWhiteSpace(eventType)) query = query.Where(e => e.EventType == eventType);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .AsNoTracking()
            .TakePage(page, pageSize.ValidPageSizeOr(DefaultPageSize))
            .ToListAsync(ct);
        return (items, total);
    }

    /// <summary>
    /// Games, days and types are filtered in SQL; the exact (GameId, ScoringDay) pairing in memory
    /// (EF can't translate a Contains over tuples), over at most games x distinct-days rows per type.
    /// </summary>
    public async Task<int> SumAmountByUserAndGameDaysAsync(int userId, IReadOnlyDictionary<int, DateTime> dayByGame, IReadOnlyCollection<string> eventTypes, CancellationToken ct)
    {
        if (dayByGame == null || dayByGame.Count == 0 || eventTypes == null || eventTypes.Count == 0) return 0;

        var gameIds = dayByGame.Keys.ToList();
        var days = dayByGame.Values.Select(d => d.Date).Distinct().ToList();
        var types = eventTypes.Distinct().ToList();
        var events = await db.XpEvents
            .Where(e => e.UserId == userId
                     && e.GameId != null && gameIds.Contains(e.GameId.Value)
                     && e.ScoringDay != null && days.Contains(e.ScoringDay.Value)
                     && types.Contains(e.EventType))
            .Select(e => new { GameId = e.GameId.GetValueOrDefault(), ScoringDay = e.ScoringDay.GetValueOrDefault(), e.Amount })
            .ToListAsync(ct);

        return events
            .Where(e => e.ScoringDay.Date == dayByGame[e.GameId].Date)
            .Sum(e => e.Amount);
    }

    public Task<bool> HasDayWinAsync(int userId, int gameId, DateTime scoringDay, CancellationToken ct)
    {
        var day = scoringDay.Date;
        return db.XpEvents.AnyAsync(e => e.UserId == userId && e.GameId == gameId && e.ScoringDay == day && e.EventType == XpEventTypes.DayWin, ct);
    }

    public Task<XpEvent?> GetDayWinAsync(int userId, int gameId, DateTime scoringDay, CancellationToken ct)
    {
        var day = scoringDay.Date;
        return db.XpEvents.FirstOrDefaultAsync(e => e.UserId == userId && e.GameId == gameId && e.ScoringDay == day && e.EventType == XpEventTypes.DayWin, ct);
    }

    public async Task RemoveAsync(XpEvent xpEvent, CancellationToken ct)
    {
        db.XpEvents.Remove(xpEvent);
        await db.SaveChangesAsync(ct);
    }

    public Task SetScoringDayForSubmissionAsync(int submissionId, DateTime scoringDay, CancellationToken ct)
    {
        DateTime? eventDay = scoringDay;
        return db.XpEvents.Where(e => e.SubmissionId == submissionId)
            .ExecuteUpdateAsync(set => set.SetProperty(e => e.ScoringDay, eventDay), ct);
    }

    public async Task<XpEvent> AddAsync(XpEvent xpEvent, CancellationToken ct)
    {
        db.XpEvents.Add(xpEvent);
        await db.SaveChangesAsync(ct);
        return xpEvent;
    }
}
