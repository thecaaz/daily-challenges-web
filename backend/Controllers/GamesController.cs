using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DailyChallenges.Mapping;
using DailyChallenges.Services;
using DailyChallenges.DTOs;
using DailyChallenges.Exceptions;

namespace DailyChallenges.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GamesController(
    IGameQueryService gameQueries,
    IGameCommandService gameCommands,
    IGameHighscoreService highscores) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var dtos = await gameQueries.GetAllAsync(User);
        return Ok(dtos);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromForm] GameCreateRequest request)
    {
        RejectLegacyTimezoneOffset();
        var created = await gameCommands.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("{id}/image")]
    public async Task<IActionResult> GetImage(int id)
    {
        var (data, contentType) = await gameQueries.GetImageAsync(id);
        if (data == null || data.Length == 0) return NotFound();
        var ct = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        return File(data, ct);
    }

    [HttpGet("{id}/highscore")]
    public async Task<IActionResult> GetHighscore(int id)
    {
        var res = await highscores.GetHighscoreAsync(id, User);
        return Ok(new { highscore = res.Highscore, top = res.Top });
    }

    [HttpGet("{id}/personal-highscore")]
    [Authorize]
    public async Task<IActionResult> GetPersonalHighscore(int id)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Forbid();
        var res = await highscores.GetPersonalHighscoreAsync(id, userId.Value);
        return Ok(new { personalHighscore = res.Highscore, top = res.Top });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromForm] GameUpdateRequest request)
    {
        RejectLegacyTimezoneOffset();
        var updated = await gameCommands.UpdateAsync(id, request);
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await gameCommands.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>
    /// resetTime is UTC "HH:mm" only. An old cached SPA still sends a local time plus
    /// resetTimezoneOffsetMinutes; reject it instead of silently storing a shifted reset time.
    /// (Kept for one release after the field was removed.)
    /// </summary>
    private void RejectLegacyTimezoneOffset()
    {
        if (Request.HasFormContentType && Request.Form.ContainsKey("resetTimezoneOffsetMinutes"))
            throw new ValidationException("resetTimezoneOffsetMinutes is no longer supported: send resetTime as UTC \"HH:mm\". Reload the page to get the current app.");
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var overview = await gameQueries.GetOverviewAsync(id, User);
        if (overview == null || overview.Game == null) return NotFound();
        return Ok(overview.Game);
    }

    [HttpGet("{id}/overview")]
    public async Task<IActionResult> GetOverview(int id, [FromQuery] string? include)
    {
        var dto = await gameQueries.GetOverviewAsync(id, User, include);
        return Ok(dto);
    }
}
