using DailyChallenges.Data;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public sealed class EfNotificationRepository(AppDbContext db) : INotificationRepository
{
    private const int DefaultPageSize = 20;

    /// <summary>Newest first. A page below 1 reads as the first page, a page size below 1 as <see cref="DefaultPageSize"/>.</summary>
    public async Task<(List<Notification> Items, int TotalCount, int UnreadCount)> GetByUserPagedAsync(int userId, int page, int pageSize, CancellationToken ct)
    {
        var forUser = db.Notifications.Where(n => n.UserId == userId);
        var total = await forUser.CountAsync(ct);
        var unread = await forUser.CountAsync(n => !n.IsRead, ct);
        var items = await forUser
            .OrderByDescending(n => n.CreatedAt)
            .TakePage(page, pageSize.ValidPageSizeOr(DefaultPageSize))
            .AsNoTracking()
            .ToListAsync(ct);

        return (items, total, unread);
    }

    public Task<int> GetUnreadCountAsync(int userId, CancellationToken ct) =>
        db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public async Task MarkReadAsync(int id, int userId, CancellationToken ct)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);
        if (notification == null) return;

        notification.IsRead = true;
        await db.SaveChangesAsync(ct);
    }

    public Task MarkAllReadAsync(int userId, CancellationToken ct) =>
        db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);

    public async Task CreateBatchAsync(List<Notification> notifications, CancellationToken ct)
    {
        if (notifications.Count == 0) return;

        db.Notifications.AddRange(notifications);
        await db.SaveInsertsOrDetachAsync(notifications, ct);
    }

    public Task<bool> HasRecentUnreadFromActorAsync(RecentNotificationQuery query, CancellationToken ct)
    {
        var (userId, type, actorUserId, sinceUtc) = query;
        return db.Notifications.AnyAsync(n =>
            n.UserId == userId &&
            n.Type == type &&
            n.ActorUserId == actorUserId &&
            !n.IsRead &&
            n.CreatedAt >= sinceUtc, ct);
    }

    public Task<Notification?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (notification == null) return;

        db.Notifications.Remove(notification);
        await db.SaveChangesAsync(ct);
    }

    public Task<int> DeleteDayResultNotificationsAsync(int gameId, DateTime scoringDay, CancellationToken ct)
    {
        var day = scoringDay.Date;
        return db.Notifications
            .Where(n => n.GameId == gameId && n.ScoringDay == day
                && (n.Type == NotificationTypes.DayWin || n.Type == NotificationTypes.DayPlacement))
            .ExecuteDeleteAsync(ct);
    }
}
