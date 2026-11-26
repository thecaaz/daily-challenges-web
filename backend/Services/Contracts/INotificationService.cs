using DailyChallenges.DTOs;

namespace DailyChallenges.Services;

public interface INotificationService
{
    /// <summary>Newest first. Out-of-range paging values are clamped (see <see cref="Paging"/>).</summary>
    Task<NotificationPageDto> GetNotificationsAsync(int userId, int page, int pageSize, CancellationToken ct);

    Task<int> GetUnreadCountAsync(int userId, CancellationToken ct);

    Task MarkReadAsync(int id, int userId, CancellationToken ct);

    Task MarkAllReadAsync(int userId, CancellationToken ct);

    /// <summary>
    /// Admins may delete any notification, everyone else only their own. Throws NotFoundException if it is
    /// missing or (for non-admins) someone else's.
    /// </summary>
    Task DeleteAsync(int id, RequestingUser requester, CancellationToken ct);
}
