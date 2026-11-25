using DailyChallenges.Models;

namespace DailyChallenges.Services.Dashboard;

/// <summary>
/// Everything the dashboard sections share about "today" for one viewer. Every game has its own current
/// scoring day (<see cref="ScoringDayByGame"/>), so "today's" submissions of a game are those on that day.
/// </summary>
/// <param name="FetchedSubmissions">All submissions on any of the games' current days, including those
/// of a game whose own current day is a different date.</param>
/// <param name="TodaySubmissionsByGame">Per game, the submissions on that game's current scoring day.</param>
/// <param name="GamesViewerPlayedToday">Games the viewer has submitted to on their current scoring day.</param>
public sealed record DashboardTodaySnapshot(
    int ViewerId,
    DateTime Now,
    List<Game> Games,
    IReadOnlyDictionary<int, DateTime> ScoringDayByGame,
    List<Submission> FetchedSubmissions,
    Dictionary<int, List<Submission>> TodaySubmissionsByGame,
    HashSet<int> GamesViewerPlayedToday,
    HashSet<int> FavoriteGameIds)
{
    public Dictionary<int, Game> GameById { get; } = Games.ToDictionary(g => g.Id);

    public bool IsOnItsGamesCurrentDay(Submission submission) =>
        ScoringDayByGame.TryGetValue(submission.GameId, out var day) && submission.ScoringDay == day;

    public Game? FindGame(int gameId) => GameById.GetValueOrDefault(gameId);
}
