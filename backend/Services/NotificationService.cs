using DailyChallenges.DTOs;
using DailyChallenges.Exceptions;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;

namespace DailyChallenges.Services;

public sealed class NotificationService(INotificationRepository notifications) : INotificationService
{
    private const string NotFoundMessage = "Notification not found";

    public async Task<NotificationPageDto> GetNotificationsAsync(int userId, int page, int pageSize)
    {
        var (clampedPage, clampedPageSize) = Paging.Clamp(page, pageSize);
        var (items, total, unread) = await notifications.GetByUserPagedAsync(userId, clampedPage, clampedPageSize);

        return new NotificationPageDto
        {
            Items = items.Select(DtoMapper.ToDto).ToList(),
            TotalCount = total,
            UnreadCount = unread
        };
    }

    public Task<int> GetUnreadCountAsync(int userId) => notifications.GetUnreadCountAsync(userId);

    public Task MarkReadAsync(int id, int userId) => notifications.MarkReadAsync(id, userId);

    public Task MarkAllReadAsync(int userId) => notifications.MarkAllReadAsync(userId);

    /// <summary>Someone else's notification reads as "not found" so ids cannot be probed.</summary>
    public async Task DeleteOwnAsync(int id, int userId)
    {
        var notification = await GetExistingAsync(id);
        if (notification.UserId != userId)
            throw new NotFoundException(NotFoundMessage);
        await notifications.DeleteAsync(id);
    }

    public async Task DeleteAnyAsync(int id)
    {
        await GetExistingAsync(id);
        await notifications.DeleteAsync(id);
    }

    private async Task<Notification> GetExistingAsync(int id) =>
        await notifications.GetByIdAsync(id) ?? throw new NotFoundException(NotFoundMessage);
}
