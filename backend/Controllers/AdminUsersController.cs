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

            int? adminId = null;
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(idClaim) && int.TryParse(idClaim, out var parsed)) adminId = parsed;

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
            const int MaxExportRows = 10000;
            var (items, total) = await _adminService.GetXpEventsAsync(id, 1, MaxExportRows, from, to, eventType);

            var sb = new StringBuilder();
            sb.AppendLine("Id,UserId,SubmissionId,GameId,ScoringDay,Amount,EventType,Details,CreatedAt");

            string Escape(string? s)
            {
                if (string.IsNullOrEmpty(s)) return string.Empty;
                return '"' + s.Replace("\"", "\"\"") + '"';
            }

            foreach (var it in items)
            {
                var scoringDay = it.ScoringDay.HasValue ? it.ScoringDay.Value.ToString("yyyy-MM-dd") : string.Empty;
                var createdAt = it.CreatedAt.ToString("o");
                var line = string.Join(',', new string[] {
                    it.Id.ToString(),
                    it.UserId.ToString(),
                    it.SubmissionId?.ToString() ?? string.Empty,
                    it.GameId?.ToString() ?? string.Empty,
                    scoringDay,
                    it.Amount.ToString(),
                    Escape(it.EventType),
                    Escape(it.Details),
                    Escape(createdAt)
                });
                sb.AppendLine(line);
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv; charset=utf-8", $"xp-events-user-{id}.csv");
        }
    }
}
