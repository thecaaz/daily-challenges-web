using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        private readonly AppDbContext _db;
        private readonly IXpService _xpService;
        private readonly LevelCalculator _levelCalc;

        public AdminUsersController(AppDbContext db, IXpService xpService, LevelCalculator levelCalc)
        {
            _db = db;
            _xpService = xpService;
            _levelCalc = levelCalc;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;

            var q = _db.Users.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                q = q.Where(u => u.Username.Contains(search));
            }

            var total = await q.CountAsync();
            var users = await q.OrderBy(u => u.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var items = users.Select(u => DtoMapper.ToDto(u, _levelCalc)).ToList();
            return Ok(new { items, totalCount = total });
        }

        public record AdjustXpDto(int Delta, string? Reason);

        [HttpPost("{id}/xp")]
        public async Task<IActionResult> AdjustXp(int id, [FromBody] AdjustXpDto dto)
        {
            if (dto == null) return BadRequest();
            if (dto.Delta == 0) return BadRequest(new { message = "Delta must be non-zero" });

            int? adminId = null;
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(idClaim) && int.TryParse(idClaim, out var parsed)) adminId = parsed;

            try
            {
                await _xpService.AdjustXpAsync(id, dto.Delta, dto.Reason ?? "admin_adjustment", adminId);
                var user = await _db.Users.FindAsync(id);
                if (user == null) return NotFound();
                return Ok(DtoMapper.ToDto(user, _levelCalc));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
    }
}
