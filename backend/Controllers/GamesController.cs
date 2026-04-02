using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DailyChallenges.Mapping;
using DailyChallenges.Services;

namespace DailyChallenges.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GamesController : ControllerBase
    {
        private readonly IGameService _games;

        public GamesController(IGameService games)
        {
            _games = games;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var dtos = await _games.GetAllAsync();
            return Ok(dtos);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromForm] string name, [FromForm] IFormFile? image, [FromForm] string? resetTime, [FromForm] string? resetTimezoneId, [FromForm] string? url, [FromForm] string? description, [FromForm] string? rankingMode)
        {
            if (string.IsNullOrWhiteSpace(name)) return BadRequest("name is required");
            var created = await _games.CreateAsync(name, image, resetTime, resetTimezoneId, url, description, rankingMode);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }

        [HttpGet("{id}/image")]
        public async Task<IActionResult> GetImage(int id)
        {
            var (data, contentType) = await _games.GetImageAsync(id);
            if (data == null || data.Length == 0) return NotFound();
            var ct = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
            return File(data, ct);
        }

        [HttpGet("{id}/highscore")]
        public async Task<IActionResult> GetHighscore(int id)
        {
            var res = await _games.GetHighscoreAsync(id, User);
            return Ok(new { highscore = res.Highscore, top = res.Top });
        }

        [HttpGet("{id}/personal-highscore")]
        [Authorize]
        public async Task<IActionResult> GetPersonalHighscore(int id)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            var res = await _games.GetPersonalHighscoreAsync(id, userId.Value);
            return Ok(new { personalHighscore = res.Highscore, top = res.Top });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromForm] string? name, [FromForm] IFormFile? image, [FromForm] string? resetTime, [FromForm] string? resetTimezoneId, [FromForm] string? url, [FromForm] string? description, [FromForm] string? rankingMode)
        {
            var updated = await _games.UpdateAsync(id, name, image, resetTime, resetTimezoneId, url, description, rankingMode);
            return Ok(updated);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            await _games.DeleteAsync(id);
            return NoContent();
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var g = await _games.GetByIdAsync(id);
            if (g == null) return NotFound();
            return Ok(DtoMapper.ToDto(g));
        }

        [HttpGet("{id}/overview")]
        public async Task<IActionResult> GetOverview(int id, [FromQuery] string? include, [FromQuery] int? top)
        {
            var dto = await _games.GetOverviewAsync(id, User, include, top ?? 0);
            return Ok(dto);
        }
    }
}
