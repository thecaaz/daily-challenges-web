using DailyChallenges.Models;
using DailyChallenges.Repositories;

namespace DailyChallenges.Services.Dashboard;

public sealed class DashboardTodaySnapshotLoader(
    IGameRepository games,
    ISubmissionRepository submissions,
    IFavoriteRepository favorites,
    TimeProvider time,
    ILogger<DashboardTodaySnapshotLoader> logger)
{
    public async Task<DashboardTodaySnapshot> LoadAsync(int viewerId)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var favoriteGameIds = (await favorites.GetFavoriteGameIdsForUserAsync(viewerId)).ToHashSet();
        var allGames = await games.GetAllAsync();
        var scoringDayByGame = GetCurrentScoringDays(allGames, now);
        var fetched = await submissions.GetByScoringDaysAsync(scoringDayByGame.Values.Distinct().ToList());

        var todaySubmissions = fetched
            .Where(s => scoringDayByGame.TryGetValue(s.GameId, out var day) && s.ScoringDay == day)
            .ToList();

        return new DashboardTodaySnapshot(
            viewerId,
            now,
            allGames,
            scoringDayByGame,
            fetched,
            todaySubmissions.GroupBy(s => s.GameId).ToDictionary(g => g.Key, g => g.ToList()),
            todaySubmissions.Where(s => s.UserId == viewerId).Select(s => s.GameId).ToHashSet(),
            favoriteGameIds);
    }

    private Dictionary<int, DateTime> GetCurrentScoringDays(List<Game> allGames, DateTime now) =>
        allGames.ToDictionary(g => g.Id, g => GetCurrentScoringDay(g, now));

    /// <summary>Only a timestamp at the edge of the DateTime range can fail; the dashboard then falls back to the UTC date.</summary>
    private DateTime GetCurrentScoringDay(Game game, DateTime now)
    {
        try
        {
            return ScoringDayCalculator.GetCurrentScoringDay(game.ResetTime, now).Date;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            logger.LogWarning(ex, "Failed to compute scoring day for game {GameId}; falling back to UTC.Date", game.Id);
            return now.Date;
        }
    }
}
