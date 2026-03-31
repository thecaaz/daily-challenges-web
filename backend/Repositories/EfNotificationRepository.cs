using DailyChallenges.Data;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories
{
    public class EfNotificationRepository : INotificationRepository
    {
        private readonly AppDbContext _db;
        public EfNotificationRepository(AppDbContext db) => _db = db;

        public async Task<(List<Notification> Items, int TotalCount, int UnreadCount)> GetByUserPagedAsync(int userId, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var q = _db.Notifications.Where(n => n.UserId == userId);
            var total = await q.CountAsync();
            var unread = await q.CountAsync(n => !n.IsRead);

            var items = await q
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();

            return (items, total, unread);
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task MarkReadAsync(int id, int userId)
        {
            var n = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
            if (n == null) return;
            n.IsRead = true;
            await _db.SaveChangesAsync();
        }

        public async Task MarkAllReadAsync(int userId)
        {
            await _db.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        }

        public async Task CreateBatchAsync(List<Notification> notifications)
        {
            if (notifications.Count == 0) return;
            _db.Notifications.AddRange(notifications);
            await _db.SaveChangesAsync();
        }
    }
}
