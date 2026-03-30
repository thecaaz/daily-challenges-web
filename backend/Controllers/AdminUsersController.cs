using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using DailyChallenges.Data;
using DailyChallenges.Services;
using DailyChallenges.Mapping;

namespace DailyChallenges.Controllers
{
    [ApiController]
    [Route("api/admin/users")]
    [Authorize(Roles = "Admin")]
    public class AdminUsersController : ControllerBase
    {
        private readonly IAdminUserService _adminService;

        public AdminUsersController(IAdminUserService adminService)
        {
            _adminService = adminService;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null)
        {
            var (items, total) = await _adminService.GetUsersAsync(page, pageSize, search);
            return Ok(new { items, totalCount = total });
        }

        [HttpPost("{id}/xp")]
        public async Task<IActionResult> AdjustXp(int id, [FromBody] DailyChallenges.DTOs.AdminAdjustXpDto dto)
        {
            if (dto == null) return BadRequest();
            if (dto.Delta == 0) return BadRequest(new { message = "Delta must be non-zero" });

            var adminId = User.GetUserId();

            try
            {
                var updated = await _adminService.AdjustXpAsync(id, dto.Delta, dto.Reason, adminId);
                return Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpGet("{id}/xp-events")]
        public async Task<IActionResult> GetXpEvents(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] string? eventType = null)
        {
            var (items, total) = await _adminService.GetXpEventsAsync(id, page, pageSize, from, to, eventType);
            return Ok(new { items, totalCount = total });
        }

        [HttpGet("{id}/xp-events/export")]
        public async Task<IActionResult> ExportXpEvents(int id, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] string? eventType = null)
        {
            var (data, filename) = await _adminService.ExportXpEventsCsvAsync(id, from, to, eventType);
            return File(data, "text/csv; charset=utf-8", filename);
        }
    }
}
