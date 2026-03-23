using Microsoft.AspNetCore.Mvc;
using DailyChallenges.Data;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using DailyChallenges.Repositories;

namespace DailyChallenges.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GamesController : ControllerBase
    {
        private readonly IGameRepository _games;
        private readonly IWebHostEnvironment _env;

        public GamesController(IGameRepository games, IWebHostEnvironment env)
        {
            _games = games;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var games = await _games.GetAllAsync();
            var dtos = games.Select(g => DailyChallenges.Mapping.DtoMapper.ToDto(g)).ToList();
            return Ok(dtos);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromForm] string name, [FromForm] IFormFile? image, [FromForm] string? resetTime, [FromForm] string? resetTimezoneId, [FromForm] string? url)
        {
            if (string.IsNullOrWhiteSpace(name)) return BadRequest("name is required");

            var game = new Game { Name = name };

            if (image != null && image.Length > 0)
            {
                using var ms = new MemoryStream();
                await image.CopyToAsync(ms);
                game.ScreenshotData = ms.ToArray();
                game.ScreenshotContentType = image.ContentType;
            }

            // parse reset time (expecting HH:mm) and optional timezone id
            if (!string.IsNullOrWhiteSpace(resetTime))
            {
                if (TimeSpan.TryParse(resetTime, out var ts))
                {
                    game.ResetTime = ts;
                }
                else if (TimeSpan.TryParseExact(resetTime, "hh\\:mm", null, out var ts2))
                {
                    game.ResetTime = ts2;
                }
            }
            if (!string.IsNullOrWhiteSpace(resetTimezoneId))
            {
                game.ResetTimezoneId = resetTimezoneId;
            }

            if (!string.IsNullOrWhiteSpace(url))
            {
                game.Url = url;
            }

            var created = await _games.CreateAsync(game);
            var dto = DailyChallenges.Mapping.DtoMapper.ToDto(created);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, dto);
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
    }
}
