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
        private readonly Services.ISubmissionService _subs;
        private readonly Services.IFileValidator _validator;

        public SubmissionsController(Services.ISubmissionService subs, Services.IFileValidator validator)
        {
            _subs = subs;
            _validator = validator;
        }

        [HttpGet("game/{gameId}")]
        public async Task<IActionResult> GetByGame(int gameId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var pageResult = await _subs.GetByGameAsync(gameId, User, page, pageSize);
            return Ok(pageResult);
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
