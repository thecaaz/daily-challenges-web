using DailyChallenges.Exceptions;
using DailyChallenges.Repositories;

namespace DailyChallenges.Services;

/// <summary>
/// Recomputes Submission.ScoringDay (and the matching XpEvents.ScoringDay) of every submission of a game
/// after its last finalized day, for a new reset time. Finalized days are never touched, so results, win XP
/// and notifications stay consistent. Runs inside the caller's transaction and throws a ConflictException
/// instead of moving anything into a finalized day or merging two submissions of one user into one day.
/// </summary>
public sealed class GameScoringDayRederiver(
    IScoringDayResultRepository results,
    ISubmissionRepository submissions,
    IXpEventRepository xpEvents,
    TimeProvider time)
{
    public const string MergeConflictMessage =
        "Changing the reset time would merge two submissions of the same user into one scoring day.";

    private static readonly DateTime PlaceholderBaseDay = new(1000, 1, 1);

    private sealed record ScoringDayMove(int SubmissionId, int? UserId, DateTime OldDay, DateTime NewDay);

    public async Task RederiveAsync(int gameId, TimeSpan newResetTime, CancellationToken ct)
    {
        var lastFinalized = await results.GetLastFinalizedDayAsync(gameId, ct);
        EnsureCurrentDayIsNotFinalized(newResetTime, lastFinalized);

        var moves = await PlanMovesAsync(gameId, newResetTime, lastFinalized, ct);
        EnsureNoMoveIntoFinalizedDay(moves, lastFinalized);
        EnsureNoUserHasTwoSubmissionsOnOneDay(moves);

        var changed = moves.Where(m => m.NewDay != m.OldDay).ToList();
        if (changed.Count == 0) return;

        await ParkOnPlaceholderDaysAsync(changed, ct);
        await MoveToNewDaysAsync(changed, ct);
    }

    /// <summary>The new current day must not be a finalized day either, or new submissions would land in it.</summary>
    private void EnsureCurrentDayIsNotFinalized(TimeSpan newResetTime, DateTime? lastFinalized)
    {
        var newCurrentDay = ScoringDayCalculator.GetCurrentScoringDay(newResetTime, time.GetUtcNow().UtcDateTime).Date;
        if (lastFinalized.HasValue && newCurrentDay <= lastFinalized.Value)
            throw new ConflictException($"With this reset time the current scoring day would be {ScoringDayCalculator.FormatScoringDay(newCurrentDay)}, which is already finalized. Try again after the new reset time has passed.");
    }

    private async Task<List<ScoringDayMove>> PlanMovesAsync(int gameId, TimeSpan newResetTime, DateTime? lastFinalized, CancellationToken ct)
    {
        var rows = await submissions.GetScoringDayRowsAsync(gameId, lastFinalized, ct);
        return rows
            .Select(r => new ScoringDayMove(r.Id, r.UserId, r.ScoringDay.Date, ScoringDayCalculator.GetScoringDay(r.CreatedAt, newResetTime).Date))
            .ToList();
    }

    private static void EnsureNoMoveIntoFinalizedDay(List<ScoringDayMove> moves, DateTime? lastFinalized)
    {
        if (lastFinalized.HasValue && moves.Any(m => m.NewDay <= lastFinalized.Value))
            throw new ConflictException("Changing the reset time would move submissions into an already finalized scoring day.");
    }

    private static void EnsureNoUserHasTwoSubmissionsOnOneDay(List<ScoringDayMove> moves)
    {
        if (moves.Where(m => m.UserId.HasValue).GroupBy(m => (m.UserId, m.NewDay)).Any(group => group.Count() > 1))
            throw new ConflictException(MergeConflictMessage);
    }

    /// <summary>
    /// First of two passes, so the (UserId, GameId, ScoringDay) unique index never sees a transient duplicate
    /// (SQLite checks it row by row): each moved row is parked on its own placeholder day.
    /// </summary>
    private async Task ParkOnPlaceholderDaysAsync(List<ScoringDayMove> changed, CancellationToken ct)
    {
        for (var i = 0; i < changed.Count; i++)
            await submissions.SetScoringDayAsync(changed[i].SubmissionId, PlaceholderBaseDay.AddDays(i), ct);
    }

    private async Task MoveToNewDaysAsync(List<ScoringDayMove> changed, CancellationToken ct)
    {
        foreach (var move in changed)
        {
            await submissions.SetScoringDayAsync(move.SubmissionId, move.NewDay, ct);
            await xpEvents.SetScoringDayForSubmissionAsync(move.SubmissionId, move.NewDay, ct);
        }
    }
}
