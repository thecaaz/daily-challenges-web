using DailyChallenges.Data;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public sealed class EfNotificationRepository(AppDbContext db) : INotificationRepository
{
    private const int DefaultPageSize = 20;

    /// <summary>Newest first. A page below 1 reads as the first page, a page size below 1 as <see cref="DefaultPageSize"/>.</summary>
    public async Task<(List<Notification> Items, int TotalCount, int UnreadCount)> GetByUserPagedAsync(int userId, int page, int pageSize)
    {
        var forUser = db.Notifications.Where(n => n.UserId == userId);
        var total = await forUser.CountAsync();
        var unread = await forUser.CountAsync(n => !n.IsRead);
        var items = await forUser
            .OrderByDescending(n => n.CreatedAt)
            .TakePage(page, pageSize.ValidPageSizeOr(DefaultPageSize))
            .AsNoTracking()
            .ToListAsync();

        return (items, total, unread);
    }

    public Task<int> GetUnreadCountAsync(int userId) =>
        db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

    public async Task MarkReadAsync(int id, int userId)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (notification == null) return;

        notification.IsRead = true;
        await db.SaveChangesAsync();
    }

    public Task MarkAllReadAsync(int userId) =>
        db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

    public async Task CreateBatchAsync(List<Notification> notifications)
    {
        if (notifications.Count == 0) return;

        db.Notifications.AddRange(notifications);
        await db.SaveInsertsOrDetachAsync(notifications);
    }

    public Task<bool> HasRecentUnreadFromActorAsync(RecentNotificationQuery query)
    {
        var (userId, type, actorUserId, sinceUtc) = query;
        return db.Notifications.AnyAsync(n =>
            n.UserId == userId &&
            n.Type == type &&
            n.ActorUserId == actorUserId &&
            !n.IsRead &&
            n.CreatedAt >= sinceUtc);
    }

    public Task<Notification?> GetByIdAsync(int id) =>
        db.Notifications.FirstOrDefaultAsync(n => n.Id == id);

    public async Task DeleteAsync(int id)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id);
        if (notification == null) return;

        db.Notifications.Remove(notification);
        await db.SaveChangesAsync();
    }

    public Task<int> DeleteDayResultNotificationsAsync(int gameId, DateTime scoringDay)
    {
        var day = scoringDay.Date;
        return db.Notifications
            .Where(n => n.GameId == gameId && n.ScoringDay == day
                && (n.Type == NotificationTypes.DayWin || n.Type == NotificationTypes.DayPlacement))
            .ExecuteDeleteAsync();
    }
}
