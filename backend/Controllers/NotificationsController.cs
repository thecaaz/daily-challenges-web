using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DailyChallenges.Services;

namespace DailyChallenges.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class NotificationsController(INotificationService notifications) : ControllerBase
{
    private const int DefaultPageSize = 20;

    [HttpGet]
    public async Task<IActionResult> GetNotifications([CurrentUserId] int userId, [FromQuery] int page = 1, [FromQuery] int pageSize = DefaultPageSize) =>
        Ok(await notifications.GetNotificationsAsync(userId, page, pageSize));

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount([CurrentUserId] int userId) =>
        Ok(new { unreadCount = await notifications.GetUnreadCountAsync(userId) });

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkRead([CurrentUserId] int userId, int id)
    {
        await notifications.MarkReadAsync(id, userId);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead([CurrentUserId] int userId)
    {
        await notifications.MarkAllReadAsync(userId);
        return NoContent();
    }

    /// <summary>Admins may delete any notification; everyone else only their own (others read as 404).</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete([CurrentUserId] int userId, int id)
    {
        if (User.IsAdmin())
            await notifications.DeleteAnyAsync(id);
        else
            await notifications.DeleteOwnAsync(id, userId);
        return NoContent();
    }
}
