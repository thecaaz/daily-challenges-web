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
            if (to.HasValue) q = q.Where(e => e.CreatedAt <= to.Value);
            if (!string.IsNullOrWhiteSpace(eventType)) q = q.Where(e => e.EventType == eventType);

            var total = await q.CountAsync();
            var skip = (page - 1) * pageSize;
            var items = await q.OrderByDescending(e => e.CreatedAt).AsNoTracking().Skip(skip).Take(pageSize).ToListAsync();
            return (items, total);
        }

        public async Task<int> SumAmountByUserAndDaysAndTypesAsync(int userId, List<DateTime> scoringDays, string[] eventTypes)
        {
            if (scoringDays == null || scoringDays.Count == 0) return 0;
            // EF Core 7 on .NET 10 has a JIT bug (InvalidProgramException) when compiling
            // DynamicMethods to extract captured collection parameters (List<DateTime>, string[])
            // from expression trees. Fetch only by userId in SQL and filter the rest in-memory.
            var days = scoringDays.Select(d => d.Date).ToHashSet();
            var types = new HashSet<string>(eventTypes);
            var events = await _db.XpEvents
                .Where(e => e.UserId == userId && e.ScoringDay.HasValue)
                .Select(e => new { ScoringDay = e.ScoringDay.GetValueOrDefault(), e.Amount, e.EventType })
                .ToListAsync();
            return events
                .Where(e => types.Contains(e.EventType) && days.Contains(e.ScoringDay.Date))
                .Sum(e => e.Amount);
        }

        public async Task<XpEvent> AddAsync(XpEvent xpEvent)
        {
            _db.XpEvents.Add(xpEvent);
            await _db.SaveChangesAsync();
            return xpEvent;
        }
    }
}
