using DailyChallenges.DTOs;

namespace DailyChallenges.Services
{
    public interface INotificationService
    {
        Task<NotificationPageDto> GetNotificationsAsync(int userId, int page = 1, int pageSize = 20);

        Task<int> GetUnreadCountAsync(int userId);

        Task MarkReadAsync(int id, int userId);

        Task MarkAllReadAsync(int userId);

        /// <summary>
        /// Deletes a notification if the requesting user is the owner or an admin.
        /// Throws NotFoundException if not found or not owned by the caller (unless admin).
        /// </summary>
        Task DeleteAsync(int id, int requestingUserId, bool isAdmin);
    }
}
