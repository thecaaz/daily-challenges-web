using DailyChallenges.Models;

namespace DailyChallenges.Repositories
{
    public interface INotificationRepository
    {
        Task<(List<Notification> Items, int TotalCount, int UnreadCount)> GetByUserPagedAsync(int userId, int page, int pageSize);
        Task<int> GetUnreadCountAsync(int userId);
        Task MarkReadAsync(int id, int userId);
        Task MarkAllReadAsync(int userId);
        Task CreateBatchAsync(List<Notification> notifications);
        /// <summary>True when <paramref name="userId"/> has an unread notification of this type caused by this actor, created at or after <paramref name="sinceUtc"/>.</summary>
        Task<bool> HasRecentUnreadFromActorAsync(int userId, string type, int actorUserId, DateTime sinceUtc);
        Task<Notification?> GetByIdAsync(int id);
        Task DeleteAsync(int id);
        /// <summary>Deletes the finalizer's "day_win"/"day_placement" notifications of a game's scoring day; returns the count.</summary>
        Task<int> DeleteDayResultNotificationsAsync(int gameId, DateTime scoringDay);
    }
}
