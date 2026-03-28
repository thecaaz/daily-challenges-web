using Microsoft.AspNetCore.Mvc;
using System.Globalization;
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
        private readonly Services.ISubmissionService _subs;
        private readonly Services.IFileValidator _validator;

        public SubmissionsController(Services.ISubmissionService subs, Services.IFileValidator validator)
        {
            _subs = subs;
            _validator = validator;
        }

        [HttpGet("game/{gameId}")]
        public async Task<IActionResult> GetByGame(int gameId, [FromQuery] string? scoringDay = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            DateTime? parsedDay = null;
            if (!string.IsNullOrWhiteSpace(scoringDay))
            {
                if (!DateTime.TryParseExact(scoringDay, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    return BadRequest(new { message = "Invalid scoringDay format (expected yyyy-MM-dd)" });
                parsedDay = dt;
            }

            var pageResult = await _subs.GetByGameAsync(gameId, User, parsedDay, page, pageSize);
            return Ok(pageResult);
        }

        [HttpGet("game/{gameId}/available-dates")]
        public async Task<IActionResult> GetAvailableDates(int gameId)
        {
            var dates = await _subs.GetAvailableDatesAsync(gameId);
            return Ok(dates);
        }

        [HttpGet("game/{gameId}/has-submitted")]
        public async Task<IActionResult> HasSubmittedForLatest(int gameId)
        {
            var has = await _subs.HasUserSubmittedForLatestAsync(gameId, User);
            return Ok(new { hasSubmittedForLatest = has });
        }

        [HttpGet("game/{gameId}/winner")]
        public async Task<IActionResult> GetWinner(int gameId, [FromQuery] string? scoringDay = null)
        {
            DateTime? parsedDay = null;
            if (!string.IsNullOrWhiteSpace(scoringDay))
            {
                if (!DateTime.TryParseExact(scoringDay, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    return BadRequest(new { message = "Invalid scoringDay format (expected yyyy-MM-dd)" });
                parsedDay = dt;
            }

            // Request full-day results so the service can compute the winner
            var pageResult = await _subs.GetByGameAsync(gameId, User, parsedDay, 1, int.MaxValue / 4);
            var winner = pageResult.Items?.FirstOrDefault(i => i.IsDayWinner);
            if (winner == null) return NotFound();
            return Ok(winner);
        }

        [HttpGet("game/{gameId}/unfiltered")]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUnfilteredByGame(int gameId)
        {
            var list = await _subs.GetUnfilteredByGameAsync(gameId);
            return Ok(list);
        }

        [HttpPost]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> Create([FromForm] int gameId, [FromForm] string score, [FromForm] string? username, [FromForm] IFormFile? screenshot)
        {
            try
            {
                var created = await _subs.CreateAsync(gameId, score, username, screenshot, User);
                return CreatedAtAction(nameof(GetByGame), new { gameId = gameId }, created);
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message.Contains("already submitted") ? Conflict(new { message = ex.Message }) : BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}/screenshot")]
        public async Task<IActionResult> GetScreenshot(int id)
        {
            var (data, contentType) = await _subs.GetScreenshotAsync(id);
            if (data == null) return NotFound();
            return File(data, contentType ?? "application/octet-stream");
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var dto = await _subs.GetByIdAsync(id);
            if (dto == null) return NotFound();
            return Ok(dto);
        }

        [HttpPut("{id}")]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] SubmissionUpdateModel model)
        {
            try
            {
                var updated = await _subs.UpdateAsync(id, model.Score);
                return Ok(updated);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
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
    }
}
