using Microsoft.AspNetCore.Mvc;
using DailyChallenges.Data;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using DailyChallenges.Repositories;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Security.Claims;

namespace DailyChallenges.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GamesController : ControllerBase
    {
        private readonly Services.IGameService _games;
        private readonly Services.IFileValidator _validator;

        public GamesController(Services.IGameService games, Services.IFileValidator validator)
        {
            _games = games;
            _validator = validator;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var dtos = await _games.GetAllAsync();
            return Ok(dtos);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromForm] string name, [FromForm] IFormFile? image, [FromForm] string? resetTime, [FromForm] string? resetTimezoneId, [FromForm] string? url)
        {
            if (string.IsNullOrWhiteSpace(name)) return BadRequest("name is required");
            try
            {
                var created = await _games.CreateAsync(name, image, resetTime, resetTimezoneId, url);
                return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}/image")]
        public async Task<IActionResult> GetImage(int id)
        {
            var g = await _games.GetByIdAsync(id);
            if (g == null) return NotFound();
            if (g.ScreenshotData == null || g.ScreenshotData.Length == 0) return NotFound();
            var contentType = string.IsNullOrWhiteSpace(g.ScreenshotContentType) ? "application/octet-stream" : g.ScreenshotContentType;
            return File(g.ScreenshotData, contentType);
        }

        [HttpGet("{id}/highscore")]
        public async Task<IActionResult> GetHighscore(int id)
        {
            try
            {
                var res = await _games.GetHighscoreAsync(id, User);
                return Ok(new { highscore = res.Highscore, top = res.Top });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("{id}/personal-highscore")]
        [Authorize]
        public async Task<IActionResult> GetPersonalHighscore(int id)
        {
            var userId = Services.ClaimsPrincipalExtensions.GetUserId(User);
            if (!userId.HasValue) return Forbid();
            try
            {
                var res = await _games.GetPersonalHighscoreAsync(id, userId.Value);
                return Ok(new { personalHighscore = res.Highscore, top = res.Top });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromForm] string? name, [FromForm] IFormFile? image, [FromForm] string? resetTime, [FromForm] string? resetTimezoneId, [FromForm] string? url)
        {
            try
            {
                var updated = await _games.UpdateAsync(id, name, image, resetTime, resetTimezoneId, url);
                return Ok(updated);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            await _games.DeleteAsync(id);
            return NoContent();
        }
    }
}
