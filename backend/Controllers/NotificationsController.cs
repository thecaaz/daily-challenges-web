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
    public async Task<IActionResult> GetNotifications([CurrentUserId] int userId, [FromQuery] int page = 1, [FromQuery] int pageSize = DefaultPageSize, CancellationToken ct = default) =>
        Ok(await notifications.GetNotificationsAsync(userId, page, pageSize, ct));

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount([CurrentUserId] int userId, CancellationToken ct) =>
        Ok(new { unreadCount = await notifications.GetUnreadCountAsync(userId, ct) });

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkRead([CurrentUserId] int userId, int id, CancellationToken ct)
    {
        await notifications.MarkReadAsync(id, userId, ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead([CurrentUserId] int userId, CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(userId, ct);
        return NoContent();
    }

    /// <summary>Admins may delete any notification; everyone else only their own (others read as 404).</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete([CurrentUserId] int userId, int id, CancellationToken ct)
    {
        await notifications.DeleteAsync(id, new RequestingUser(userId, User.IsAdmin()), ct);
        return NoContent();
    }
}
