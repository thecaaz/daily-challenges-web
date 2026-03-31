using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DailyChallenges.Repositories;
using DailyChallenges.DTOs;
using DailyChallenges.Services;

namespace DailyChallenges.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationRepository _notifications;

        public NotificationsController(INotificationRepository notifications)
        {
            _notifications = notifications;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();

            var (items, total, unread) = await _notifications.GetByUserPagedAsync(userId.Value, page, pageSize);

            var dto = new NotificationPageDto
            {
                Items = items.Select(n => new NotificationDto
                {
                    Id = n.Id,
                    Message = n.Message,
                    Type = n.Type,
                    GameId = n.GameId,
                    ScoringDay = ScoringDayHelper.FormatScoringDay(n.ScoringDay),
                    Rank = n.Rank,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                }).ToList(),
                TotalCount = total,
                UnreadCount = unread
            };

            return Ok(dto);
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();

            var count = await _notifications.GetUnreadCountAsync(userId.Value);
            return Ok(new { unreadCount = count });
        }

        [HttpPost("{id}/read")]
        public async Task<IActionResult> MarkRead(int id)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();

            await _notifications.MarkReadAsync(id, userId.Value);
            return NoContent();
        }

        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();

            await _notifications.MarkAllReadAsync(userId.Value);
            return NoContent();
        }
    }
}
