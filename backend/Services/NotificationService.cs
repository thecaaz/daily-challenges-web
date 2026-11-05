using DailyChallenges.Exceptions;
using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Repositories;

namespace DailyChallenges.Services
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _notifications;

        public NotificationService(INotificationRepository notifications)
        {
            _notifications = notifications;
        }

        public async Task<NotificationPageDto> GetNotificationsAsync(int userId, int page = 1, int pageSize = 20)
        {
            var (items, total, unread) = await _notifications.GetByUserPagedAsync(userId, page, pageSize);

            return new NotificationPageDto
            {
                Items = items.Select(DtoMapper.ToDto).ToList(),
                TotalCount = total,
                UnreadCount = unread
            };
        }

        public Task<int> GetUnreadCountAsync(int userId) => _notifications.GetUnreadCountAsync(userId);

        public Task MarkReadAsync(int id, int userId) => _notifications.MarkReadAsync(id, userId);

        public Task MarkAllReadAsync(int userId) => _notifications.MarkAllReadAsync(userId);

        public async Task DeleteAsync(int id, int requestingUserId, bool isAdmin)
        {
            var n = await _notifications.GetByIdAsync(id);
            if (n == null) throw new NotFoundException("Notification not found");
            // Someone else's notification reads as "not found" so ids cannot be probed.
            if (n.UserId != requestingUserId && !isAdmin) throw new NotFoundException("Notification not found");
            await _notifications.DeleteAsync(id);
        }
    }
}
