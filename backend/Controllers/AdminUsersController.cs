using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DailyChallenges.DTOs;
using DailyChallenges.Services;

namespace DailyChallenges.Controllers;

/// <summary>Admin user management. A token without a user id is answered with 401 here, not 403.</summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public sealed class AdminUsersController(
    IAdminUserService adminUsers,
    IAdminXpEventService adminXpEvents,
    IAdminUserDeletionService adminUserDeletion) : ControllerBase
{
    private const int DefaultUserPageSize = 50;
    private const int DefaultXpEventPageSize = 20;

    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] int page = Paging.FirstPage, [FromQuery] int pageSize = DefaultUserPageSize, [FromQuery] string? search = null)
    {
        var (clampedPage, clampedPageSize) = Paging.Clamp(page, pageSize);
        var (items, total) = await adminUsers.GetUsersAsync(clampedPage, clampedPageSize, search);
        return Ok(new { items, totalCount = total });
    }

    [HttpPost("{id}/xp")]
    public async Task<IActionResult> AdjustXp(int id, [FromBody] AdminAdjustXpDto dto)
    {
        if (dto == null) return BadRequest();

        return Ok(await adminUsers.AdjustXpAsync(new XpAdjustment(id, dto.Delta, dto.Reason, User.GetUserId())));
    }

    /// <summary>`from` is inclusive, `to` is exclusive.</summary>
    [HttpGet("{id}/xp-events")]
    public async Task<IActionResult> GetXpEvents(int id, [FromQuery] int page = Paging.FirstPage, [FromQuery] int pageSize = DefaultXpEventPageSize, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] string? eventType = null)
    {
        var (clampedPage, clampedPageSize) = Paging.Clamp(page, pageSize);
        var (items, total) = await adminXpEvents.GetXpEventsAsync(new XpEventQuery(id, from, to, eventType), clampedPage, clampedPageSize);
        return Ok(new { items, totalCount = total });
    }

    [HttpGet("{id}/xp-events/export")]
    public async Task<IActionResult> ExportXpEvents(int id, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] string? eventType = null)
    {
        var (data, filename) = await adminXpEvents.ExportXpEventsCsvAsync(new XpEventQuery(id, from, to, eventType));
        return File(data, ContentTypes.CsvUtf8, filename);
    }

    /// <summary>The service validates the password (400) and refuses another admin's account (403).</summary>
    [HttpPost("{id}/password")]
    public async Task<IActionResult> SetPassword([CurrentUserId(WhenMissing = MissingUserIdResponse.Unauthorized)] int requestingAdminId, int id, [FromBody] AdminSetPasswordDto dto)
    {
        await adminUsers.SetPasswordAsync(id, dto?.NewPassword, requestingAdminId);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser([CurrentUserId(WhenMissing = MissingUserIdResponse.Unauthorized)] int requestingAdminId, int id)
    {
        await adminUserDeletion.DeleteUserAsync(id, requestingAdminId);
        return NoContent();
    }
}
