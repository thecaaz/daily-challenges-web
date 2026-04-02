using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DailyChallenges.Services;

namespace DailyChallenges.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();

            var dto = await _notificationService.GetNotificationsAsync(userId.Value, page, pageSize);
            return Ok(dto);
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();

            var count = await _notificationService.GetUnreadCountAsync(userId.Value);
            return Ok(new { unreadCount = count });
        }

        [HttpPost("{id}/read")]
        public async Task<IActionResult> MarkRead(int id)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();

            await _notificationService.MarkReadAsync(id, userId.Value);
            return NoContent();
        }

        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();

            await _notificationService.MarkAllReadAsync(userId.Value);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();

            try
            {
                await _notificationService.DeleteAsync(id, userId.Value, User.IsInRole("Admin"));
                return NoContent();
            }
            catch (InvalidOperationException)
            {
                return Forbid();
            }
        }
    }
}
