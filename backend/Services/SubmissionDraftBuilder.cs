using DailyChallenges.Exceptions;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using System.Security.Claims;

namespace DailyChallenges.Services;

public sealed record SubmissionDraftRequest(int GameId, string Score, string? Username, IFormFile? Screenshot);

/// <summary>
/// Validates a new submission and builds the unsaved entity. The scoring day is fixed at write time so
/// reads can query it directly.
/// </summary>
public sealed class SubmissionDraftBuilder(
    IGameRepository games,
    ISubmissionRepository submissions,
    IFileStorage files,
    TimeProvider time)
{
    public const string AlreadySubmittedMessage = "User has already submitted for this game";
    private const string ScreenshotRequiredMessage = "screenshot is required";

    public async Task<Submission> BuildAsync(SubmissionDraftRequest request, ClaimsPrincipal user)
    {
        var game = await games.GetByIdWithoutBlobAsync(request.GameId) ?? throw new ValidationException("invalid gameId");
        if (string.IsNullOrWhiteSpace(request.Score)) throw new ValidationException("score is required");

        await EnsureFirstSubmissionOfDayAsync(user.GetUserId(), game);
        var submission = NewSubmission(game, request, user);
        await AttachScreenshotAsync(submission, request.Screenshot);
        return submission;
    }

    /// <summary>One submission per user per game per scoring day; anonymous submissions are not limited.</summary>
    private async Task EnsureFirstSubmissionOfDayAsync(int? userId, Game game)
    {
        if (userId is not int signedInUserId) return;

        var currentDay = ScoringDayCalculator.GetCurrentScoringDay(game.ResetTime, time.GetUtcNow().UtcDateTime).Date;
        if (await submissions.ExistsForUserGameDayAsync(signedInUserId, game.Id, currentDay))
            throw new ConflictException(AlreadySubmittedMessage);
    }

    /// <summary>Unparseable score text is kept with a null ScoreValue: stored, but unranked.</summary>
    private Submission NewSubmission(Game game, SubmissionDraftRequest request, ClaimsPrincipal user)
    {
        var score = request.Score.Trim();
        var userId = user.GetUserId();
        var createdAt = time.GetUtcNow().UtcDateTime;
        return new Submission
        {
            GameId = game.Id,
            Score = score,
            ScoreValue = ScoreParser.TryParseDouble(score, out var scoreValue) ? scoreValue : null,
            Username = GetDisplayedUsername(userId, request.Username, user),
            UserId = userId,
            CreatedAt = createdAt,
            ScoringDay = ScoringDayCalculator.GetScoringDay(createdAt, game.ResetTime),
        };
    }

    /// <summary>Signed-in users are listed under their account name; anonymous ones under the name they typed.</summary>
    private static string? GetDisplayedUsername(int? userId, string? requestedUsername, ClaimsPrincipal user)
    {
        var accountName = user.Identity?.Name;
        return userId.HasValue && !string.IsNullOrEmpty(accountName) ? accountName : requestedUsername;
    }

    private async Task AttachScreenshotAsync(Submission submission, IFormFile? screenshot)
    {
        if (screenshot is not { Length: > 0 }) throw new ArgumentException(ScreenshotRequiredMessage);

        var (data, contentType) = await files.ReadFileAsync(screenshot);
        if (data is not { Length: > 0 }) throw new ArgumentException(ScreenshotRequiredMessage);

        submission.ScreenshotData = data;
        submission.ScreenshotContentType = contentType ?? string.Empty;
    }
}
