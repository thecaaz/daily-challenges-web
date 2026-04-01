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
        Task<Notification?> GetByIdAsync(int id);
        Task DeleteAsync(int id);
    }
}
