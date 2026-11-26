namespace DailyChallenges.Services;

public interface ISubmissionDeletionService
{
    /// <summary>
    /// Admin delete: revokes the submission's XP, deletes it and, when it changes a finalized day's winner,
    /// re-finalizes the day. The user may submit again for that day. Deleting a missing submission is a no-op.
    /// </summary>
    Task DeleteAsync(int id, CancellationToken ct);
}
