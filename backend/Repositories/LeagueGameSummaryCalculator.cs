using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Services;
using DailyChallenges.Services.Ranking;

namespace DailyChallenges.Repositories;

/// <summary>A league member's submission, projected without screenshot blobs.</summary>
internal sealed record LeagueSubmissionRow(
    int GameId,
    string GameName,
    bool GameHasIcon,
    RankingMode GameRankingMode,
    TimeSpan GameResetTime,
    int UserId,
    string Score,
    double? ScoreValue,
    DateTime ScoringDay,
    DateTime CreatedAt);

/// <summary>
/// Builds one game's <see cref="LeagueGameSummaryDto"/> as seen by the requesting member, over the last
/// <c>days</c> scoring days.
/// </summary>
/// <remarks>
/// Hidden-scores rule: for games the requester has not submitted for on the game's current scoring day
/// (not in <c>gamesRequesterPlayedToday</c>), other members' current-day rows do not count towards
/// TopScore / MyRank.
/// </remarks>
internal sealed class LeagueGameSummaryCalculator(int requestingUserId, int days, IReadOnlySet<int> gamesRequesterPlayedToday)
{
    /// <param name="gameRows">The game's submissions within the window, all for the same game.</param>
    /// <param name="currentScoringDay">The game's current scoring day.</param>
    public LeagueGameSummaryDto Summarize(IReadOnlyList<LeagueSubmissionRow> gameRows, DateTime currentScoringDay)
    {
        var game = gameRows[0];
        var rankingMode = game.GameRankingMode;
        var hidesOthersCurrentDayScores = !gamesRequesterPlayedToday.Contains(game.GameId);
        var countedRows = gameRows
            .Where(r => r.ScoreValue.HasValue)
            .Where(r => !hidesOthersCurrentDayScores || r.UserId == requestingUserId || r.ScoringDay.Date != currentScoringDay)
            .ToList();
        var top = BestFirst(countedRows, rankingMode).FirstOrDefault();
        var myBest = BestFirst(gameRows.Where(r => r.UserId == requestingUserId && r.ScoreValue.HasValue), rankingMode).FirstOrDefault();
        var recentDays = RecentScoringDays(currentScoringDay);

        return new LeagueGameSummaryDto
        {
            GameId = game.GameId,
            GameName = game.GameName,
            IconUrl = game.GameHasIcon ? GameImageUrl.Of(game.GameId) : null,
            LastPlayedAt = gameRows.Max(r => r.CreatedAt),
            PlayCount = gameRows.Count,
            TopScore = top?.Score,
            TopScoreValue = top?.ScoreValue,
            MyBestScore = myBest?.Score,
            MyBestScoreValue = myBest?.ScoreValue,
            MyRank = RankOfRequester(countedRows, rankingMode),
            RecentPlays = recentDays.Select(day => gameRows.Count(r => r.ScoringDay.Date == day)).ToList(),
            RecentPlayDays = recentDays.Select(ScoringDayCalculator.FormatScoringDay).ToList(),
        };
    }

    private int? RankOfRequester(List<LeagueSubmissionRow> countedRows, RankingMode rankingMode)
    {
        var bestPerUserBestFirst = BestFirst(
                countedRows.GroupBy(r => r.UserId).Select(userRows => BestFirst(userRows, rankingMode).First()),
                rankingMode)
            .ToList();
        var requesterIndex = bestPerUserBestFirst.FindIndex(r => r.UserId == requestingUserId);
        if (requesterIndex < 0) return null;

        var ranks = CompetitionRanking.RanksOf(bestPerUserBestFirst.Select(r => r.ScoreValue!.Value).ToList());
        return ranks[requesterIndex];
    }

    /// <summary>One entry per scoring day of the window, oldest to newest, ending at the current day.</summary>
    private List<DateTime> RecentScoringDays(DateTime currentScoringDay)
    {
        var firstDay = currentScoringDay.AddDays(-(days - 1));
        return Enumerable.Range(0, Math.Max(0, days)).Select(offset => firstDay.AddDays(offset).Date).ToList();
    }

    private static IOrderedEnumerable<LeagueSubmissionRow> BestFirst(IEnumerable<LeagueSubmissionRow> rows, RankingMode rankingMode) =>
        rankingMode == RankingMode.Lowest
            ? rows.OrderBy(r => r.ScoreValue).ThenBy(r => r.CreatedAt)
            : rows.OrderByDescending(r => r.ScoreValue).ThenBy(r => r.CreatedAt);
}
