using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DailyChallenges.Services;

namespace DailyChallenges.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController(
    IAdminUserService adminUsers,
    IAdminXpEventService adminXpEvents,
    IAdminUserDeletionService adminUserDeletion) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null)
    {
        (page, pageSize) = Paging.Clamp(page, pageSize);
        var (items, total) = await adminUsers.GetUsersAsync(page, pageSize, search);
        return Ok(new { items, totalCount = total });
    }

    [HttpPost("{id}/xp")]
    public async Task<IActionResult> AdjustXp(int id, [FromBody] DTOs.AdminAdjustXpDto dto)
    {
        if (dto == null) return BadRequest();
        if (dto.Delta == 0) return BadRequest(new { message = "Delta must be non-zero" });

        var adminId = User.GetUserId();

        var updated = await adminUsers.AdjustXpAsync(id, dto.Delta, dto.Reason, adminId);
        return Ok(updated);
    }

    /// <summary>`from` is inclusive, `to` is exclusive.</summary>
    [HttpGet("{id}/xp-events")]
    public async Task<IActionResult> GetXpEvents(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] string? eventType = null)
    {
        (page, pageSize) = Paging.Clamp(page, pageSize);
        var (items, total) = await adminXpEvents.GetXpEventsAsync(id, page, pageSize, from, to, eventType);
        return Ok(new { items, totalCount = total });
    }

    [HttpGet("{id}/xp-events/export")]
    public async Task<IActionResult> ExportXpEvents(int id, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] string? eventType = null)
    {
        var (data, filename) = await adminXpEvents.ExportXpEventsCsvAsync(id, from, to, eventType);
        return File(data, "text/csv; charset=utf-8", filename);
    }

    /// <summary>The service validates the length (400) and refuses another admin's account (403).</summary>
    [HttpPost("{id}/password")]
    public async Task<IActionResult> SetPassword(int id, [FromBody] DTOs.AdminSetPasswordDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.NewPassword))
            return BadRequest(new { message = "NewPassword is required" });

        var requestingAdminId = User.GetUserId();
        if (requestingAdminId == null) return Unauthorized();

        await adminUsers.SetPasswordAsync(id, dto.NewPassword, requestingAdminId.Value);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var requestingAdminId = User.GetUserId();
        if (requestingAdminId == null) return Unauthorized();

        await adminUserDeletion.DeleteUserAsync(id, requestingAdminId.Value);
        return NoContent();
    }
}
