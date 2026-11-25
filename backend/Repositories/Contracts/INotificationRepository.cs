using DailyChallenges.Models;

namespace DailyChallenges.Repositories;

public interface INotificationRepository
{
    Task<(List<Notification> Items, int TotalCount, int UnreadCount)> GetByUserPagedAsync(int userId, int page, int pageSize);
    Task<int> GetUnreadCountAsync(int userId);
    Task MarkReadAsync(int id, int userId);
    Task MarkAllReadAsync(int userId);
    Task CreateBatchAsync(List<Notification> notifications);
    /// <summary>True when the user has an unread notification of this type caused by this actor, created at or after <see cref="RecentNotificationQuery.SinceUtc"/>.</summary>
    Task<bool> HasRecentUnreadFromActorAsync(RecentNotificationQuery query);
    Task<Notification?> GetByIdAsync(int id);
    Task DeleteAsync(int id);
    /// <summary>Deletes the finalizer's "day_win"/"day_placement" notifications of a game's scoring day; returns the count.</summary>
    Task<int> DeleteDayResultNotificationsAsync(int gameId, DateTime scoringDay);
}

/// <summary>A notification of <see cref="Type"/> for <see cref="UserId"/> caused by <see cref="ActorUserId"/> since <see cref="SinceUtc"/>.</summary>
public sealed record RecentNotificationQuery(int UserId, string Type, int ActorUserId, DateTime SinceUtc);
