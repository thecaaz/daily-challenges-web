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
    }
}
