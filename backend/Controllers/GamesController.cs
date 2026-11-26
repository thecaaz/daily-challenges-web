using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DailyChallenges.Services;
using DailyChallenges.DTOs;
using DailyChallenges.Exceptions;

namespace DailyChallenges.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class GamesController(
    IGameQueryService gameQueries,
    IGameCommandService gameCommands,
    IGameHighscoreService highscores) : ControllerBase
{
    private const string LegacyTimezoneOffsetField = "resetTimezoneOffsetMinutes";

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await gameQueries.GetAllAsync(User));

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
        return File(data, string.IsNullOrWhiteSpace(contentType) ? ContentTypes.OctetStream : contentType);
    }

    [HttpGet("{id}/highscore")]
    public async Task<IActionResult> GetHighscore(int id)
    {
        var result = await highscores.GetHighscoreAsync(id, User);
        return Ok(new { highscore = result.Highscore, top = result.Top });
    }

    [HttpGet("{id}/personal-highscore")]
    [Authorize]
    public async Task<IActionResult> GetPersonalHighscore([CurrentUserId] int userId, int id)
    {
        var result = await highscores.GetPersonalHighscoreAsync(id, userId);
        return Ok(new { personalHighscore = result.Highscore, top = result.Top });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromForm] GameUpdateRequest request)
    {
        RejectLegacyTimezoneOffset();
        return Ok(await gameCommands.UpdateAsync(id, request));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await gameCommands.DeleteAsync(id);
        return NoContent();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var overview = await gameQueries.GetOverviewAsync(id, User, include: null);
        if (overview?.Game == null) return NotFound();
        return Ok(overview.Game);
    }

    [HttpGet("{id}/overview")]
    public async Task<IActionResult> GetOverview(int id, [FromQuery] string? include) =>
        Ok(await gameQueries.GetOverviewAsync(id, User, include));

    /// <summary>
    /// resetTime is UTC "HH:mm" only. An old cached SPA still sends a local time plus
    /// resetTimezoneOffsetMinutes; reject it instead of silently storing a shifted reset time.
    /// (Kept for one release after the field was removed.)
    /// </summary>
    private void RejectLegacyTimezoneOffset()
    {
        if (Request.HasFormContentType && Request.Form.ContainsKey(LegacyTimezoneOffsetField))
            throw new ValidationException("resetTimezoneOffsetMinutes is no longer supported: send resetTime as UTC \"HH:mm\". Reload the page to get the current app.");
    }
}
