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

        [HttpGet("{id}/highscore")]
        public async Task<IActionResult> GetHighscore(int id)
        {
            var g = await _games.GetByIdAsync(id);
            if (g == null) return NotFound();

            var subs = g.Submissions ?? new List<Submission>();

            double ParseScore(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return double.NaN;
                var m = Regex.Match(s, "-?\\d+(?:[.,]\\d+)?");
                if (!m.Success) return double.NaN;
                var raw = m.Value.Replace(',', '.');
                if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
                return double.NaN;
            }

            // Order by parsed numeric score descending (higher is better). If parsing fails, treat as very low.
            var ordered = subs
                .Select(s => new { Sub = s, Num = ParseScore(s.Score) })
                .OrderByDescending(x => double.IsNaN(x.Num) ? double.NegativeInfinity : x.Num)
                .ThenBy(x => x.Sub.CreatedAt)
                .Take(50)
                .Select(x => DailyChallenges.Mapping.DtoMapper.ToDto(x.Sub))
                .ToList();

            var top = ordered.FirstOrDefault();
            return Ok(new { highscore = top, top = ordered });
        }

        [HttpGet("{id}/personal-highscore")]
        [Authorize]
        public async Task<IActionResult> GetPersonalHighscore(int id)
        {
            var g = await _games.GetByIdAsync(id);
            if (g == null) return NotFound();

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId)) return Forbid();

            var subs = g.Submissions?.Where(s => s.UserId == userId).ToList() ?? new List<Submission>();

            double ParseScore(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return double.NaN;
                var m = Regex.Match(s, "-?\\d+(?:[.,]\\d+)?");
                if (!m.Success) return double.NaN;
                var raw = m.Value.Replace(',', '.');
                if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
                return double.NaN;
            }

            var ordered = subs
                .Select(s => new { Sub = s, Num = ParseScore(s.Score) })
                .OrderByDescending(x => double.IsNaN(x.Num) ? double.NegativeInfinity : x.Num)
                .ThenBy(x => x.Sub.CreatedAt)
                .Take(50)
                .Select(x => DailyChallenges.Mapping.DtoMapper.ToDto(x.Sub))
                .ToList();

            var best = ordered.FirstOrDefault();
            return Ok(new { personalHighscore = best, top = ordered });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromForm] string? name, [FromForm] IFormFile? image, [FromForm] string? resetTime, [FromForm] string? resetTimezoneId, [FromForm] string? url)
        {
            var g = await _games.GetByIdAsync(id);
            if (g == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(name)) g.Name = name;
            if (!string.IsNullOrWhiteSpace(url)) g.Url = url;
            if (!string.IsNullOrWhiteSpace(resetTime))
            {
                if (TimeSpan.TryParse(resetTime, out var ts)) g.ResetTime = ts;
                else if (TimeSpan.TryParseExact(resetTime, "hh\\:mm", null, out var ts2)) g.ResetTime = ts2;
            }
            if (!string.IsNullOrWhiteSpace(resetTimezoneId)) g.ResetTimezoneId = resetTimezoneId;

            if (image != null && image.Length > 0)
            {
                using var ms = new MemoryStream();
                await image.CopyToAsync(ms);
                g.ScreenshotData = ms.ToArray();
                g.ScreenshotContentType = image.ContentType;
            }

            var updated = await _games.UpdateAsync(g);
            return Ok(DailyChallenges.Mapping.DtoMapper.ToDto(updated));
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
