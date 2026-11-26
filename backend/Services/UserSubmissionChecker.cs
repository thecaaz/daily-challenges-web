using System.Security.Claims;
using DailyChallenges.Repositories;

namespace DailyChallenges.Services;

public sealed class UserSubmissionChecker(
    ISubmissionRepository submissions,
    IGameRepository games,
    TimeProvider time) : IUserSubmissionChecker
{
    /// <summary>A missing game falls back to a midnight UTC reset.</summary>
    public async Task<bool> HasUserSubmittedForLatestAsync(int gameId, ClaimsPrincipal? user, CancellationToken ct)
    {
        if (user.GetUserId() is not int userId) return false;

        var game = await games.GetByIdWithoutBlobAsync(gameId);
        var currentDay = ScoringDayCalculator.GetCurrentScoringDay(game?.ResetTime ?? TimeSpan.Zero, time.GetUtcNow().UtcDateTime);
        return await submissions.ExistsForUserGameDayAsync(userId, gameId, currentDay);
    }

    public async Task<bool> HasUserSubmittedForDayAsync(int? userId, int gameId, DateTime scoringDay, CancellationToken ct) =>
        userId.HasValue && await submissions.ExistsForUserGameDayAsync(userId.Value, gameId, scoringDay);
}
