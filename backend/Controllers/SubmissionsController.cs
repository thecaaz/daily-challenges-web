using Microsoft.AspNetCore.Mvc;
using DailyChallenges.Data;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
using DailyChallenges.Repositories;

namespace DailyChallenges.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubmissionsController : ControllerBase
    {
        private readonly ISubmissionRepository _subs;
        private readonly IGameRepository _games;
        private readonly IWebHostEnvironment _env;

        public SubmissionsController(ISubmissionRepository subs, IGameRepository games, IWebHostEnvironment env)
        {
            _subs = subs;
            _games = games;
            _env = env;
        }

        [HttpGet("game/{gameId}")]
        public async Task<IActionResult> GetByGame(int gameId)
        {
            var subs = await _subs.GetByGameAsync(gameId);
            var dtos = subs.Select(s => DailyChallenges.Mapping.DtoMapper.ToDto(s)).ToList();
            return Ok(dtos);
        }

        [HttpPost]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> Create([FromForm] int gameId, [FromForm] string score, [FromForm] string? username, [FromForm] IFormFile? screenshot)
        {
            var game = await _games.GetByIdAsync(gameId);
            if (game == null) return BadRequest("invalid gameId");
            if (string.IsNullOrWhiteSpace(score)) return BadRequest("score is required");

            // get authenticated user id if present
            int? userId = null;
            if (User.Identity?.IsAuthenticated ?? false)
            {
                var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(idClaim, out var parsed)) userId = parsed;
            }

            // If authenticated, prevent multiple submissions per user per game
            if (userId.HasValue)
            {
                var existing = await _subs.GetByGameAndUserAsync(gameId, userId.Value);
                if (existing != null) return Conflict(new { message = "User has already submitted for this game" });
            }

            var submission = new Submission { GameId = gameId, Score = score, Username = username, UserId = userId };

            if (screenshot != null && screenshot.Length > 0)
            {
                using var ms = new MemoryStream();
                await screenshot.CopyToAsync(ms);
                submission.ScreenshotData = ms.ToArray();
                submission.ScreenshotContentType = screenshot.ContentType;
            }

            // if authenticated, prefer authoritative username from Users table
            if (userId.HasValue)
            {
                // load user name from DB to avoid spoofing
                var u = await (_env.ApplicationName != null ? Task.FromResult((DailyChallenges.Models.User?)null) : Task.FromResult((DailyChallenges.Models.User?)null));
                // We don't have direct DbContext here; just rely on User.Identity.Name when present
                if (!string.IsNullOrEmpty(User.Identity?.Name)) submission.Username = User.Identity.Name;
            }

            var created = await _subs.CreateAsync(submission);
            var dto = DailyChallenges.Mapping.DtoMapper.ToDto(created);
            return CreatedAtAction(nameof(GetByGame), new { gameId = gameId }, dto);
        }

        [HttpGet("{id}/screenshot")]
        public async Task<IActionResult> GetScreenshot(int id)
        {
            var s = await _subs.GetByIdAsync(id);
            if (s == null || s.ScreenshotData == null) return NotFound();
            return File(s.ScreenshotData, s.ScreenshotContentType ?? "application/octet-stream");
        }

        [HttpPut("{id}")]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] SubmissionUpdateModel model)
        {
            var s = await _subs.GetByIdAsync(id);
            if (s == null) return NotFound();
            if (!string.IsNullOrWhiteSpace(model.Score)) s.Score = model.Score;
            s.Username = model.Username;
            var updated = await _subs.UpdateAsync(s);
            return Ok(DailyChallenges.Mapping.DtoMapper.ToDto(updated));
        }

        [HttpDelete("{id}")]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            await _subs.DeleteAsync(id);
            return NoContent();
        }
    }

    public class SubmissionUpdateModel
    {
        public string? Score { get; set; }
        public string? Username { get; set; }
    }
}
