using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;

namespace DailyChallenges.Services.Dashboard;

/// <summary>The games played most today (on their own current scoring day), busiest first.</summary>
public static class DashboardRecentGames
{
    public const int MaxGames = 10;
    public const int MaxUsernamesPerGame = 20;
    public const string AnonymousUsername = "Anonymous";

    public static List<GameActivityDto> Build(DashboardTodaySnapshot today) =>
        today.Games
            .Where(g => today.TodaySubmissionsByGame.ContainsKey(g.Id))
            .Select(g => ToActivityDto(today, g, today.TodaySubmissionsByGame[g.Id]))
            .OrderByDescending(x => x.TodayCount)
            .Take(MaxGames)
            .ToList();

    private static GameActivityDto ToActivityDto(DashboardTodaySnapshot today, Game game, List<Submission> todaySubmissions)
    {
        var dto = DtoMapper.ToDto(game, nowUtc: today.Now);
        dto.IsFavorite = today.FavoriteGameIds.Contains(game.Id);
        dto.HasSubmittedForLatest = today.GamesViewerPlayedToday.Contains(game.Id);

        return new GameActivityDto
        {
            Game = dto,
            TodayCount = todaySubmissions.Count,
            TodayUsernames = todaySubmissions
                .Select(s => !string.IsNullOrEmpty(s.Username) ? s.Username : AnonymousUsername)
                .Distinct()
                .Take(MaxUsernamesPerGame)
                .ToList()
        };
    }
}
