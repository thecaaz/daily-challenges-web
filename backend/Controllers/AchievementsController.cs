using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DailyChallenges.DTOs;
using DailyChallenges.Achievements;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Controllers
{
    [ApiController]
    [Route("api")]
    public class AchievementsController : ControllerBase
    {
        private readonly IAchievementService _achievements;

        public AchievementsController(IAchievementService achievements)
        {
            _achievements = achievements;
        }

        /// <summary>Returns the full achievement catalog with the user's unlock state merged in.</summary>
        [HttpGet("users/{userId:int}/achievements")]
        public async Task<ActionResult<List<AchievementDto>>> GetForUser(int userId)
        {
            var result = await _achievements.GetForUserAsync(userId);
            return Ok(result);
        }

        /// <summary>Returns the full achievement catalog (no unlock state). Useful for listing all achievements.</summary>
        [HttpGet("achievements")]
        public ActionResult<List<object>> GetCatalog()
        {
            var catalog = AchievementCatalog.All.Select(a => new
            {
                achievementId = a.Id,
                a.Name,
                a.Description,
                a.IconKey
            });
            return Ok(catalog);
        }

        /// <summary>Returns the authenticated user's achievements (convenience endpoint).</summary>
        [HttpGet("users/me/achievements")]
        [Authorize]
        public async Task<ActionResult<List<AchievementDto>>> GetMine()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(claim) || !int.TryParse(claim, out var userId))
                return Unauthorized();

            var result = await _achievements.GetForUserAsync(userId);
            return Ok(result);
        }
    }
}
