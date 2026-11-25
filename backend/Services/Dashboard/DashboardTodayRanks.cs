using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;

namespace DailyChallenges.Services.Dashboard;

/// <summary>
/// The viewer's live rank on each game they scored on today. The rank matches leaderboard ordering:
/// best ScoreValue first, ties broken by the earliest CreatedAt; unscored submissions rank last.
/// </summary>
public static class DashboardTodayRanks
{
    public static List<UserTodayRankDto> Compute(DashboardTodaySnapshot today)
    {
        var viewerSubmissionByGame = ViewerSubmissionByGame(today);

        return today.GamesViewerPlayedToday
            .Where(gameId => viewerSubmissionByGame.TryGetValue(gameId, out var own) && own.ScoreValue.HasValue)
            .Select(gameId => ToRankDto(today, gameId, viewerSubmissionByGame[gameId]))
            .OrderBy(r => r.Rank)
            .ToList();
    }

    private static Dictionary<int, Submission> ViewerSubmissionByGame(DashboardTodaySnapshot today) =>
        today.FetchedSubmissions
            .Where(s => s.UserId == today.ViewerId && today.IsOnItsGamesCurrentDay(s))
            .GroupBy(s => s.GameId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreatedAt).First());

    private static UserTodayRankDto ToRankDto(DashboardTodaySnapshot today, int gameId, Submission own)
    {
        var game = today.FindGame(gameId);
        var gameSubmissions = today.TodaySubmissionsByGame.GetValueOrDefault(gameId) ?? new List<Submission>();
        var mode = game?.RankingMode ?? RankingMode.Highest;

        return new UserTodayRankDto
        {
            GameId = gameId,
            GameName = game?.Name ?? string.Empty,
            GameImageUrl = GameImageUrl.For(gameId, game?.ScreenshotContentType),
            Score = own.Score,
            Rank = gameSubmissions.Count(other => RanksAhead(other, own, mode)) + 1,
            TotalSubmissions = gameSubmissions.Count(s => s.ScoreValue.HasValue)
        };
    }

    private static bool RanksAhead(Submission other, Submission own, RankingMode mode)
    {
        var ownScore = own.ScoreValue.GetValueOrDefault();
        var isTieDecidedByTime = other.ScoreValue == own.ScoreValue && other.CreatedAt < own.CreatedAt;
        var hasBetterScore = mode == RankingMode.Highest
            ? (other.ScoreValue ?? double.MinValue) > ownScore
            : (other.ScoreValue ?? double.MaxValue) < ownScore;
        return hasBetterScore || isTieDecidedByTime;
    }
}
