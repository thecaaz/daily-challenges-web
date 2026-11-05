using DailyChallenges.Data;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories
{
    public class EfXpEventRepository : IXpEventRepository
    {
        private readonly AppDbContext _db;
        public EfXpEventRepository(AppDbContext db) => _db = db;

        public async Task<(List<XpEvent> Items, int TotalCount)> GetByUserPagedAsync(int userId, int page, int pageSize, DateTime? from = null, DateTime? to = null, string? eventType = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            IQueryable<XpEvent> q = _db.XpEvents.Where(e => e.UserId == userId);

            if (from.HasValue) q = q.Where(e => e.CreatedAt >= from.Value);
            // `to` is exclusive: the client sends the day after the last day it wants.
            if (to.HasValue) q = q.Where(e => e.CreatedAt < to.Value);
            if (!string.IsNullOrWhiteSpace(eventType)) q = q.Where(e => e.EventType == eventType);

            var total = await q.CountAsync();
            var skip = (page - 1) * pageSize;
            var items = await q.OrderByDescending(e => e.CreatedAt).AsNoTracking().Skip(skip).Take(pageSize).ToListAsync();
            return (items, total);
        }

        public async Task<int> SumAmountByUserAndGameDaysAsync(int userId, IReadOnlyDictionary<int, DateTime> dayByGame, IReadOnlyCollection<string> eventTypes)
        {
            if (dayByGame == null || dayByGame.Count == 0 || eventTypes == null || eventTypes.Count == 0) return 0;

            // Games, days and types are filtered in SQL; the exact (GameId, ScoringDay) pairing in memory
            // (EF can't translate a Contains over tuples), over at most games x distinct-days rows per type.
            var gameIds = dayByGame.Keys.ToList();
            var days = dayByGame.Values.Select(d => d.Date).Distinct().ToList();
            var types = eventTypes.Distinct().ToList();
            var events = await _db.XpEvents
                .Where(e => e.UserId == userId
                         && e.GameId != null && gameIds.Contains(e.GameId.Value)
                         && e.ScoringDay != null && days.Contains(e.ScoringDay.Value)
                         && types.Contains(e.EventType))
                .Select(e => new { GameId = e.GameId.GetValueOrDefault(), ScoringDay = e.ScoringDay.GetValueOrDefault(), e.Amount })
                .ToListAsync();

            return events
                .Where(e => e.ScoringDay.Date == dayByGame[e.GameId].Date)
                .Sum(e => e.Amount);
        }

        public Task<bool> HasDayWinAsync(int userId, int gameId, DateTime scoringDay)
        {
            var day = scoringDay.Date;
            return _db.XpEvents.AnyAsync(e => e.UserId == userId && e.GameId == gameId && e.ScoringDay == day && e.EventType == "day_win");
        }

        public Task<XpEvent?> GetDayWinAsync(int userId, int gameId, DateTime scoringDay)
        {
            var day = scoringDay.Date;
            return _db.XpEvents.FirstOrDefaultAsync(e => e.UserId == userId && e.GameId == gameId && e.ScoringDay == day && e.EventType == "day_win");
        }

        public async Task RemoveAsync(XpEvent xpEvent)
        {
            _db.XpEvents.Remove(xpEvent);
            await _db.SaveChangesAsync();
        }

        public async Task<XpEvent> AddAsync(XpEvent xpEvent)
        {
            _db.XpEvents.Add(xpEvent);
            await _db.SaveChangesAsync();
            return xpEvent;
        }
    }
}
