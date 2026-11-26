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
    public async Task<IActionResult> GetByGame(int gameId, [FromQuery] string? scoringDay = null, [FromQuery] int page = Paging.FirstPage, [FromQuery] int pageSize = DefaultPageSize, CancellationToken ct = default)
    {
        if (!ScoringDayCalculator.TryParseScoringDay(scoringDay, out DateTime? parsedDay))
            return InvalidScoringDay();

        var (clampedPage, clampedPageSize) = Paging.Clamp(page, pageSize);
        return Ok(await submissionQueries.GetByGameAsync(new GameSubmissionsPageRequest(gameId, parsedDay, clampedPage, clampedPageSize), User, ct));
    }

    [HttpGet("game/{gameId}/available-dates")]
    public async Task<IActionResult> GetAvailableDates(int gameId, CancellationToken ct) =>
        Ok(await submissionQueries.GetAvailableDatesAsync(gameId, ct));

    [HttpGet("game/{gameId}/has-submitted")]
    public async Task<IActionResult> HasSubmittedForLatest(int gameId, CancellationToken ct) =>
        Ok(new { hasSubmittedForLatest = await submissionQueries.HasUserSubmittedForLatestAsync(gameId, User, ct) });

    [HttpGet("game/{gameId}/today-submitters")]
    public async Task<IActionResult> GetTodaySubmitters(int gameId, CancellationToken ct) =>
        Ok(await submissionQueries.GetTodaySubmittersAsync(gameId, ct));

    [HttpGet("game/{gameId}/winner")]
    public async Task<IActionResult> GetWinner(int gameId, [FromQuery] string? scoringDay = null, CancellationToken ct = default)
    {
        if (!ScoringDayCalculator.TryParseScoringDay(scoringDay, out DateTime? parsedDay))
            return InvalidScoringDay();

        var winner = await submissionQueries.GetWinnerAsync(gameId, parsedDay, User, ct);
        return winner == null ? NoContent() : Ok(winner);
    }

    [HttpGet("game/{gameId}/unfiltered")]
    [Authorize(Roles = AuthRoles.Admin)]
    public async Task<IActionResult> GetUnfilteredByGame(int gameId, CancellationToken ct) =>
        Ok(await submissionQueries.GetUnfilteredByGameAsync(gameId, ct));

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromForm] SubmissionCreateRequest request, CancellationToken ct)
    {
        var draft = new SubmissionDraftRequest(request.GameId, request.Score, request.Username, request.Screenshot);
        var (submissionDto, xpGain) = await submissionCreation.CreateAsync(draft, User, ct);
        var response = new { submission = submissionDto, xpGain };
        return CreatedAtAction(nameof(GetByGame), new { gameId = request.GameId }, response);
    }

    [HttpGet("{id}/screenshot")]
    public async Task<IActionResult> GetScreenshot(int id, [FromQuery] long? exp = null, [FromQuery] string? sig = null, CancellationToken ct = default)
    {
        var (data, contentType) = await submissionQueries.GetScreenshotAsync(id, exp, sig, ct);
        if (data == null) return NotFound();
        return File(data, contentType ?? ContentTypes.OctetStream);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var dto = await submissionQueries.GetByIdAsync(id, User, ct);
        return dto == null ? NotFound() : Ok(dto);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = AuthRoles.Admin)]
    public async Task<IActionResult> Update(int id, [FromBody] SubmissionUpdateModel model, CancellationToken ct) =>
        Ok(await submissionScoreEdits.UpdateAsync(id, model.Score, ct));

    [HttpDelete("{id}")]
    [Authorize(Roles = AuthRoles.Admin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await submissionDeletion.DeleteAsync(id, ct);
        return NoContent();
    }

    private BadRequestObjectResult InvalidScoringDay() => BadRequest(new { message = InvalidScoringDayMessage });
}
