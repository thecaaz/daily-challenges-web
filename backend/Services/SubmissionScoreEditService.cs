using DailyChallenges.DTOs;
using DailyChallenges.Exceptions;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;

namespace DailyChallenges.Services;

/// <summary>
/// Admin score edit. On an already finalized day, a change of winner re-finalizes the day: the old winner's
/// day_win XP and the day's result notifications are removed, then the finalizer runs again.
/// </summary>
public sealed class SubmissionScoreEditService(
    ITransactionRunner transactions,
    ISubmissionRepository submissions,
    FinalizedDayWinnerReconciler finalizedDays,
    IGameRepository games,
    IScoreVisibilityService visibility,
    IScreenshotUrlSigner screenshotSigner) : ISubmissionScoreEditService
{
    public async Task<SubmissionDto> UpdateAsync(int id, string? score, CancellationToken ct)
    {
        var (updated, needsRefinalizing) = await transactions.RunAsync(() => SaveScoreAsync(id, score, ct), ct);
        if (needsRefinalizing) await finalizedDays.RefinalizeAsync(updated.GameId, updated.ScoringDay);
        return await ToAdminDtoAsync(updated, ct);
    }

    private async Task<(Submission Updated, bool NeedsRefinalizing)> SaveScoreAsync(int id, string? score, CancellationToken ct)
    {
        var submission = await submissions.GetByIdAsync(id, ct) ?? throw new NotFoundException("Submission not found");
        if (!string.IsNullOrWhiteSpace(score)) ApplyScore(submission, score.Trim());

        var updated = await submissions.UpdateAsync(submission, ct);
        var needsRefinalizing = await finalizedDays.UnfinalizeIfWinnerChangedAsync(updated.GameId, updated.ScoringDay, ct);
        return (updated, needsRefinalizing);
    }

    /// <summary>Unparseable score text clears ScoreValue, which leaves the submission unranked.</summary>
    private static void ApplyScore(Submission submission, string score)
    {
        submission.Score = score;
        submission.ScoreValue = ScoreParser.TryParseDouble(score, out var scoreValue) ? scoreValue : null;
    }

    /// <summary>Admin-only endpoint: admins may see the current day, so its screenshot gets a signed URL.</summary>
    private async Task<SubmissionDto> ToAdminDtoAsync(Submission updated, CancellationToken ct)
    {
        var game = await games.GetByIdWithoutBlobAsync(updated.GameId, ct);
        var isCurrentDay = game != null && visibility.IsCurrentDay(game, updated.ScoringDay);
        var dto = SubmissionDtoMapper.ToDtoWithScoringDay(updated, updated.ScoringDay.Date);
        return SubmissionDtoMapper.WithScreenshotUrl(dto, isCurrentDay ? screenshotSigner.SignedUrl : screenshotSigner.UnsignedUrl);
    }
}
