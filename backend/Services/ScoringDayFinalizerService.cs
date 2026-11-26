using DailyChallenges.Achievements;
using DailyChallenges.Repositories;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Services;

public sealed class ScoringDayFinalizerService(
    IScoringDayResultRepository results,
    IGameRepository games,
    ScoringDayOutcomeRecorder recorder,
    ITransactionRunner transactions,
    IAchievementService achievements,
    ILogger<ScoringDayFinalizerService> logger) : IScoringDayFinalizerService
{
    /// <summary>
    /// Finalizes one game's scoring day in a single transaction (see <see cref="ScoringDayOutcomeRecorder"/>);
    /// BEGIN IMMEDIATE on SQLite serializes the XP read-modify-write with submissions. The existence check
    /// up front is only a cheap shortcut: inserting the result row is the race-proof claim. On any failure
    /// nothing of the day is kept and the next background tick retries. Achievements (DayWin, LevelUp) are
    /// checked after the commit, so they see the committed result row and a failed check never undoes the
    /// finalization.
    /// Call it on a fresh DI scope: the user row must not be tracked before the transaction begins.
    /// </summary>
    public async Task FinalizeScoringDayAsync(int gameId, DateTime scoringDay, CancellationToken ct)
    {
        var day = scoringDay.Date;
        if (await results.ExistsAsync(gameId, day, ct))
        {
            logger.LogDebug("Scoring day {Day} for game {GameId} already finalized, skipping", ScoringDayCalculator.FormatScoringDay(day), gameId);
            return;
        }

        var game = await games.GetByIdWithoutBlobAsync(gameId, ct);
        if (game == null)
        {
            logger.LogWarning("Game {GameId} not found during finalization", gameId);
            return;
        }

        var outcome = await transactions.RunAsync(() => recorder.RecordAsync(game, day, ct), ct);
        if (outcome == null)
        {
            logger.LogDebug("Scoring day {Day} for game {GameId} was finalized concurrently, skipping", ScoringDayCalculator.FormatScoringDay(day), gameId);
            return;
        }

        logger.LogInformation("Finalized scoring day {Day} for game {GameName} (id={GameId}) — winner: {Winner}, {Count} notifications created",
            ScoringDayCalculator.FormatScoringDay(day), game.Name, gameId,
            outcome.WinnerName ?? outcome.WinnerUserId?.ToString() ?? "none",
            outcome.NotificationCount);

        await CheckWinnerAchievementsAsync(outcome.WinnerUserId);
    }

    private async Task CheckWinnerAchievementsAsync(int? winnerUserId)
    {
        if (winnerUserId is not int userId) return;

        await achievements.CheckAndAwardAsync(userId, AchievementTrigger.DayWin);
        await achievements.CheckAndAwardAsync(userId, AchievementTrigger.LevelUp);
    }
}
