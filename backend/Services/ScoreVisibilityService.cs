using System.Security.Claims;
using DailyChallenges.Models;

namespace DailyChallenges.Services;

public sealed class ScoreVisibilityService(IUserSubmissionChecker checker, TimeProvider time) : IScoreVisibilityService
{
    public DateTime GetCurrentScoringDay(Game game) =>
        ScoringDayCalculator.GetCurrentScoringDay(game.ResetTime, time.GetUtcNow().UtcDateTime).Date;

    public bool IsCurrentDay(Game game, DateTime scoringDay) => scoringDay.Date == GetCurrentScoringDay(game);

    public Task<bool> CanSeeDayAsync(ClaimsPrincipal? user, Game game, DateTime scoringDay, CancellationToken ct) =>
        CanSeeDayAsync(ScoreViewer.From(user), game, scoringDay, ct);

    public async Task<bool> CanSeeDayAsync(ScoreViewer viewer, Game game, DateTime scoringDay, CancellationToken ct)
    {
        if (!IsCurrentDay(game, scoringDay)) return true;
        if (viewer.IsAdmin) return true;
        return await checker.HasUserSubmittedForDayAsync(viewer.UserId, game.Id, scoringDay.Date, ct);
    }
}
