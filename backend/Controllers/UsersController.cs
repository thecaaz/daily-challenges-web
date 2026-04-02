using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DailyChallenges.Services.Contracts;
using DailyChallenges.DTOs;

namespace DailyChallenges.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly IUserProfileService _profileService;

        public UsersController(IUserProfileService profileService)
        {
            _profileService = profileService;
        }

        [HttpGet("{id}/profile")]
        public async Task<ActionResult<UserProfileDto>> GetProfile(int id, int topGames = 10)
        {
            var profile = await _profileService.GetProfileAsync(id, topGames);
            if (profile == null) return NotFound();
            return Ok(profile);
        }

        [HttpGet("me/profile")]
        [Authorize]
        public async Task<ActionResult<UserProfileDto>> GetMyProfile(int topGames = 10)
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(claim)) return Unauthorized();
            if (!int.TryParse(claim, out var userId)) return Unauthorized();

            var profile = await _profileService.GetProfileAsync(userId, topGames);
            if (profile == null) return NotFound();
            return Ok(profile);
        }
    }
}
