using DailyChallenges.DTOs;

namespace DailyChallenges.Services;

public interface INotificationService
{
    /// <summary>Newest first. Out-of-range paging values are clamped (see <see cref="Paging"/>).</summary>
    Task<NotificationPageDto> GetNotificationsAsync(int userId, int page, int pageSize);

    Task<int> GetUnreadCountAsync(int userId);

    Task MarkReadAsync(int id, int userId);

    Task MarkAllReadAsync(int userId);

    /// <summary>Deletes the user's own notification. Throws NotFoundException if it is missing or someone else's.</summary>
    Task DeleteOwnAsync(int id, int userId);

    /// <summary>Deletes any user's notification (admins). Throws NotFoundException if it is missing.</summary>
    Task DeleteAnyAsync(int id);
}
