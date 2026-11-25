using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DailyChallenges.Services;
using DailyChallenges.DTOs;

namespace DailyChallenges.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubmissionsController(
    ISubmissionQueryService submissionQueries,
    ISubmissionCreationService submissionCreation,
    ISubmissionScoreEditService submissionScoreEdits,
    ISubmissionDeletionService submissionDeletion) : ControllerBase
{

    [HttpGet("game/{gameId}")]
    public async Task<IActionResult> GetByGame(int gameId, [FromQuery] string? scoringDay = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (!ScoringDayHelper.TryParseScoringDay(scoringDay, out DateTime? parsedDay))
            return BadRequest(new { message = "Invalid scoringDay format (expected yyyy-MM-dd)" });

        (page, pageSize) = Paging.Clamp(page, pageSize);
        var pageResult = await submissionQueries.GetByGameAsync(gameId, User, parsedDay, page, pageSize);
        return Ok(pageResult);
    }

    [HttpGet("game/{gameId}/available-dates")]
    public async Task<IActionResult> GetAvailableDates(int gameId)
    {
        var dates = await submissionQueries.GetAvailableDatesAsync(gameId);
        return Ok(dates);
    }

    [HttpGet("game/{gameId}/has-submitted")]
    public async Task<IActionResult> HasSubmittedForLatest(int gameId)
    {
        var has = await submissionQueries.HasUserSubmittedForLatestAsync(gameId, User);
        return Ok(new { hasSubmittedForLatest = has });
    }

    [HttpGet("game/{gameId}/today-submitters")]
    public async Task<IActionResult> GetTodaySubmitters(int gameId)
    {
        var result = await submissionQueries.GetTodaySubmittersAsync(gameId);
        return Ok(result);
    }

    [HttpGet("game/{gameId}/winner")]
    public async Task<IActionResult> GetWinner(int gameId, [FromQuery] string? scoringDay = null)
    {
        if (!ScoringDayHelper.TryParseScoringDay(scoringDay, out DateTime? parsedDay))
            return BadRequest(new { message = "Invalid scoringDay format (expected yyyy-MM-dd)" });

        var winner = await submissionQueries.GetWinnerAsync(gameId, parsedDay, User);
        return winner == null ? NoContent() : Ok(winner);
    }

    [HttpGet("game/{gameId}/unfiltered")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUnfilteredByGame(int gameId)
    {
        var list = await submissionQueries.GetUnfilteredByGameAsync(gameId);
        return Ok(list);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromForm] SubmissionCreateRequest request)
    {
        var (submissionDto, xpGain) = await submissionCreation.CreateAsync(request.GameId, request.Score, request.Username, request.Screenshot, User);
        var response = new { submission = submissionDto, xpGain };
        return CreatedAtAction(nameof(GetByGame), new { gameId = request.GameId }, response);
    }

    [HttpGet("{id}/screenshot")]
    public async Task<IActionResult> GetScreenshot(int id, [FromQuery] long? exp = null, [FromQuery] string? sig = null)
    {
        var (data, contentType) = await submissionQueries.GetScreenshotAsync(id, exp, sig);
        if (data == null) return NotFound();
        return File(data, contentType ?? "application/octet-stream");
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var dto = await submissionQueries.GetByIdAsync(id, User);
        if (dto == null) return NotFound();
        return Ok(dto);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] SubmissionUpdateModel model)
    {
        var updated = await submissionScoreEdits.UpdateAsync(id, model.Score);
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await submissionDeletion.DeleteAsync(id);
        return NoContent();
    }
}

public class SubmissionUpdateModel
{
    public string? Score { get; set; }
}
