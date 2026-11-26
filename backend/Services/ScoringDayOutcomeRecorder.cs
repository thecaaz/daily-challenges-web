using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services.Ranking;

namespace DailyChallenges.Services;

/// <summary>What finalizing a scoring day produced; logged and used for the post-commit achievement checks.</summary>
public sealed record ScoringDayOutcome(int? WinnerUserId, string? WinnerName, int NotificationCount);

/// <summary>
/// The writes of finalizing one game's scoring day: claim the day (insert the ScoringDayResult), award the
/// win XP, notify the participants. Runs inside the finalizer's transaction, so nothing is kept unless all
/// of it succeeds.
/// </summary>
public sealed class ScoringDayOutcomeRecorder(
    ISubmissionRepository submissions,
    IScoringDayResultRepository results,
    IXpService xp,
    INotificationRepository notifications,
    ScoringDayNotificationFactory notificationFactory,
    TimeProvider time,
    ILogger<ScoringDayOutcomeRecorder> logger)
{
    /// <returns>The outcome, or null when another run already claimed the day (nothing was written).</returns>
    public async Task<ScoringDayOutcome?> RecordAsync(Game game, DateTime day, CancellationToken ct)
    {
        var rankedSubmissions = await submissions.GetByGameAndDayByScoreValueAsync(
            game.Id, day, RankingStrategyFactory.GetStrategy(game.RankingMode), ct);
        var winner = rankedSubmissions.FirstOrDefault();

        if (!await ClaimDayAsync(game.Id, day, winner?.UserId, ct)) return null;

        await AwardWinXpAsync(game, day, winner?.UserId, ct);
        var notificationCount = await NotifyParticipantsAsync(game, day, rankedSubmissions, ct);
        return new ScoringDayOutcome(winner?.UserId, winner?.Username, notificationCount);
    }

    private Task<bool> ClaimDayAsync(int gameId, DateTime day, int? winnerUserId, CancellationToken ct) =>
        results.TryCreateAsync(new ScoringDayResult
        {
            GameId = gameId,
            ScoringDay = day,
            WinnerUserId = winnerUserId,
            ProcessedAt = time.GetUtcNow().UtcDateTime
        }, ct);

    private async Task AwardWinXpAsync(Game game, DateTime day, int? winnerUserId, CancellationToken ct)
    {
        if (winnerUserId is not int userId) return;

        var xpAwarded = await xp.AwardForDayWinAsync(userId, game.Id, day, ct);
        logger.LogInformation("Awarded {Xp} XP to user {UserId} for winning game {GameName} on {Day}",
            xpAwarded, userId, game.Name, ScoringDayCalculator.FormatScoringDay(day));
    }

    private async Task<int> NotifyParticipantsAsync(Game game, DateTime day, List<Submission> rankedSubmissions, CancellationToken ct)
    {
        var dayNotifications = notificationFactory.Create(game, day, rankedSubmissions);
        if (dayNotifications.Count > 0)
            await notifications.CreateBatchAsync(dayNotifications, ct);
        return dayNotifications.Count;
    }
}
