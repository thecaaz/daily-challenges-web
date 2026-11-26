using DailyChallenges.DTOs;
using DailyChallenges.Exceptions;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;

namespace DailyChallenges.Services;

public sealed class NotificationService(INotificationRepository notifications) : INotificationService
{
    private const string NotFoundMessage = "Notification not found";

    public async Task<NotificationPageDto> GetNotificationsAsync(int userId, int page, int pageSize, CancellationToken ct)
    {
        var (clampedPage, clampedPageSize) = Paging.Clamp(page, pageSize);
        var (items, total, unread) = await notifications.GetByUserPagedAsync(userId, clampedPage, clampedPageSize, ct);

        return new NotificationPageDto
        {
            Items = items.Select(DtoMapper.ToDto).ToList(),
            TotalCount = total,
            UnreadCount = unread
        };
    }

    public Task<int> GetUnreadCountAsync(int userId, CancellationToken ct) => notifications.GetUnreadCountAsync(userId, ct);

    public Task MarkReadAsync(int id, int userId, CancellationToken ct) => notifications.MarkReadAsync(id, userId, ct);

    public Task MarkAllReadAsync(int userId, CancellationToken ct) => notifications.MarkAllReadAsync(userId, ct);

    /// <summary>For non-admins, someone else's notification reads as "not found" so ids cannot be probed.</summary>
    public async Task DeleteAsync(int id, RequestingUser requester, CancellationToken ct)
    {
        var notification = await GetExistingAsync(id, ct);
        if (!requester.IsAdmin && notification.UserId != requester.UserId)
            throw new NotFoundException(NotFoundMessage);
        await notifications.DeleteAsync(id, ct);
    }

    private async Task<Notification> GetExistingAsync(int id, CancellationToken ct) =>
        await notifications.GetByIdAsync(id, ct) ?? throw new NotFoundException(NotFoundMessage);
}
