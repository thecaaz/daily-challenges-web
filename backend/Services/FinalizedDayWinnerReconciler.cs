using DailyChallenges.Repositories;
using DailyChallenges.Services.Ranking;

namespace DailyChallenges.Services;

/// <summary>
/// Keeps a finalized scoring day consistent after an admin edits or deletes one of its submissions.
/// </summary>
public sealed class FinalizedDayWinnerReconciler(
    IScoringDayResultRepository results,
    IGameRepository games,
    ISubmissionRepository submissions,
    IXpService xp,
    INotificationRepository notifications,
    IServiceScopeFactory scopeFactory,
    ILogger<FinalizedDayWinnerReconciler> logger)
{
    /// <summary>
    /// Runs inside the caller's transaction, after the change was saved. When the day is finalized and its
    /// recorded winner differs from the current leader, it revokes the old winner's day_win XP (deleting the
    /// event, so the unique index allows a new award), deletes the day's result notifications and the
    /// ScoringDayResult row. Returns true when the caller must call <see cref="RefinalizeAsync"/> after committing.
    /// </summary>
    public async Task<bool> UnfinalizeIfWinnerChangedAsync(int gameId, DateTime scoringDay, CancellationToken ct)
    {
        var day = scoringDay.Date;
        var result = await results.GetAsync(gameId, day, ct);
        if (result == null) return false;

        var game = await games.GetByIdWithoutBlobAsync(gameId, ct);
        if (game == null) return false;

        var strategy = RankingStrategyFactory.GetStrategy(game.RankingMode);
        var leader = await submissions.GetWinnerForGameAndDayAsync(gameId, day, strategy, ct);
        if (leader?.UserId == result.WinnerUserId) return false;

        if (result.WinnerUserId.HasValue)
            await xp.RevokeDayWinAsync(result.WinnerUserId.Value, gameId, day, ct);
        await notifications.DeleteDayResultNotificationsAsync(gameId, day, ct);
        await results.DeleteAsync(result, ct);
        return true;
    }

    /// <summary>
    /// Re-runs the (idempotent) finalizer for the day on a fresh scope, as the background job does.
    /// Called after the admin change has committed, so a failure here must not fail the request: the
    /// change is applied, and the day is merely left un-finalized until the background job, which
    /// rescans recent un-finalized days every tick, retries it. This is therefore an error boundary:
    /// any exception is logged as a warning and not rethrown.
    /// It takes no CancellationToken and finalizes with <see cref="CancellationToken.None"/>: once the admin
    /// change has committed, a client abort must not leave the day un-finalized until the next background tick.
    /// </summary>
    public async Task RefinalizeAsync(int gameId, DateTime scoringDay)
    {
        try
        {
            await FinalizeOnFreshScopeAsync(gameId, scoringDay.Date);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Re-finalizing game {GameId} day {Day} failed; the background job will retry it",
                gameId, ScoringDayCalculator.FormatScoringDay(scoringDay));
        }
    }

    private async Task FinalizeOnFreshScopeAsync(int gameId, DateTime day)
    {
        using var scope = scopeFactory.CreateScope();
        var finalizer = scope.ServiceProvider.GetRequiredService<IScoringDayFinalizerService>();
        await finalizer.FinalizeScoringDayAsync(gameId, day, CancellationToken.None);
    }
}
