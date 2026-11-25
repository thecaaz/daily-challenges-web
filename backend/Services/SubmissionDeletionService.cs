using DailyChallenges.Models;
using DailyChallenges.Repositories;

namespace DailyChallenges.Services;

/// <summary>
/// Admin delete. In one transaction: revokes the submission's XP (negative "submission_revoked" event),
/// deletes the row and, when the day is finalized and its winner changes, un-finalizes the day (see
/// <see cref="FinalizedDayWinnerReconciler.UnfinalizeIfWinnerChangedAsync"/>). After the commit the finalizer
/// recomputes the winner. The user may submit again for that day; the revoked event keeps their XP balance net-once.
/// </summary>
public sealed class SubmissionDeletionService(
    ITransactionRunner transactions,
    ISubmissionRepository submissions,
    IXpService xp,
    FinalizedDayWinnerReconciler finalizedDays,
    ILogger<SubmissionDeletionService> logger) : ISubmissionDeletionService
{
    private const string XpRevocationReason = "submission_deleted";

    private sealed record DeletedSubmission(int GameId, DateTime ScoringDay, bool NeedsRefinalizing);

    public async Task DeleteAsync(int id)
    {
        var deleted = await transactions.RunAsync(() => RemoveSubmissionAsync(id));
        if (deleted == null) return;

        logger.LogInformation("Deleted submission {SubmissionId} (game {GameId}, day {Day}); re-finalizing: {Refinalize}",
            id, deleted.GameId, deleted.ScoringDay.ToString("yyyy-MM-dd"), deleted.NeedsRefinalizing);

        if (deleted.NeedsRefinalizing) await finalizedDays.RefinalizeAsync(deleted.GameId, deleted.ScoringDay);
    }

    private async Task<DeletedSubmission?> RemoveSubmissionAsync(int id)
    {
        var submission = await submissions.GetByIdWithoutBlobAsync(id);
        if (submission == null) return null;

        var day = submission.ScoringDay.Date;
        await RevokeAwardedXpAsync(submission, day);
        await submissions.DeleteAsync(id);
        var needsRefinalizing = await finalizedDays.UnfinalizeIfWinnerChangedAsync(submission.GameId, day);
        return new DeletedSubmission(submission.GameId, day, needsRefinalizing);
    }

    private async Task RevokeAwardedXpAsync(Submission submission, DateTime day)
    {
        if (submission is not { UserId: int userId, XpAwarded: int xpAwarded and > 0 }) return;

        await xp.RevokeSubmissionAsync(userId, submission.Id, submission.GameId, day, xpAwarded, XpRevocationReason);
    }
}
