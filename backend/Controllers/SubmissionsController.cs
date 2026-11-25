using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DailyChallenges.Services;
using DailyChallenges.DTOs;

namespace DailyChallenges.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class SubmissionsController(
    ISubmissionQueryService submissionQueries,
    ISubmissionCreationService submissionCreation,
    ISubmissionScoreEditService submissionScoreEdits,
    ISubmissionDeletionService submissionDeletion) : ControllerBase
{
    private const int DefaultPageSize = 50;
    private const string InvalidScoringDayMessage = "Invalid scoringDay format (expected yyyy-MM-dd)";

    [HttpGet("game/{gameId}")]
    public async Task<IActionResult> GetByGame(int gameId, [FromQuery] string? scoringDay = null, [FromQuery] int page = Paging.FirstPage, [FromQuery] int pageSize = DefaultPageSize)
    {
        if (!ScoringDayHelper.TryParseScoringDay(scoringDay, out DateTime? parsedDay))
            return InvalidScoringDay();

        var (clampedPage, clampedPageSize) = Paging.Clamp(page, pageSize);
        return Ok(await submissionQueries.GetByGameAsync(gameId, User, parsedDay, clampedPage, clampedPageSize));
    }

    [HttpGet("game/{gameId}/available-dates")]
    public async Task<IActionResult> GetAvailableDates(int gameId) =>
        Ok(await submissionQueries.GetAvailableDatesAsync(gameId));

    [HttpGet("game/{gameId}/has-submitted")]
    public async Task<IActionResult> HasSubmittedForLatest(int gameId) =>
        Ok(new { hasSubmittedForLatest = await submissionQueries.HasUserSubmittedForLatestAsync(gameId, User) });

    [HttpGet("game/{gameId}/today-submitters")]
    public async Task<IActionResult> GetTodaySubmitters(int gameId) =>
        Ok(await submissionQueries.GetTodaySubmittersAsync(gameId));

    [HttpGet("game/{gameId}/winner")]
    public async Task<IActionResult> GetWinner(int gameId, [FromQuery] string? scoringDay = null)
    {
        if (!ScoringDayHelper.TryParseScoringDay(scoringDay, out DateTime? parsedDay))
            return InvalidScoringDay();

        var winner = await submissionQueries.GetWinnerAsync(gameId, parsedDay, User);
        return winner == null ? NoContent() : Ok(winner);
    }

    [HttpGet("game/{gameId}/unfiltered")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUnfilteredByGame(int gameId) =>
        Ok(await submissionQueries.GetUnfilteredByGameAsync(gameId));

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
        return File(data, contentType ?? ContentTypes.OctetStream);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var dto = await submissionQueries.GetByIdAsync(id, User);
        return dto == null ? NotFound() : Ok(dto);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] SubmissionUpdateModel model) =>
        Ok(await submissionScoreEdits.UpdateAsync(id, model.Score));

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await submissionDeletion.DeleteAsync(id);
        return NoContent();
    }

    private BadRequestObjectResult InvalidScoringDay() => BadRequest(new { message = InvalidScoringDayMessage });
}
