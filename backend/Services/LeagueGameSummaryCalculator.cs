using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Ranking;

namespace DailyChallenges.Services;

/// <summary>
/// Builds a league's per-game summaries over the last <c>days</c> scoring days, as seen by the requesting
/// member. Each game's window ends at that game's own current scoring day.
/// </summary>
/// <remarks>
/// Hidden-scores rule: for games the requester has not submitted for on the game's current scoring day,
/// other members' current-day rows do not count towards TopScore / MyRank.
/// </remarks>
internal sealed class LeagueGameSummaryCalculator(int requestingUserId, int days, DateTime nowUtc)
{
    /// <summary>
    /// A game's current scoring day is within one day of the UTC date (late resets label "today" with
    /// tomorrow's date), so this is the widest range any game's window can cover; summaries narrow it per game.
    /// </summary>
    public (DateTime First, DateTime Last) CandidateScoringDays => (nowUtc.Date.AddDays(-days), nowUtc.Date.AddDays(1));

    /// <summary>One summary per game played within its window, most recently played first.</summary>
    /// <param name="candidateRows">Member submissions within <see cref="CandidateScoringDays"/>.</param>
    public List<LeagueGameSummaryDto> SummarizeByGame(IReadOnlyList<LeagueSubmissionRow> candidateRows)
    {
        var currentDayByGame = candidateRows
            .GroupBy(r => r.GameId)
            .ToDictionary(g => g.Key, g => ScoringDayCalculator.GetCurrentScoringDay(g.First().GameResetTime, nowUtc).Date);

        return candidateRows
            .Where(r => IsWithinWindow(r.ScoringDay, currentDayByGame[r.GameId]))
            .GroupBy(r => r.GameId)
            .Select(game => Summarize(game.ToList(), currentDayByGame[game.Key]))
            .OrderByDescending(summary => summary.LastPlayedAt)
            .ToList();
    }

    private bool IsWithinWindow(DateTime scoringDay, DateTime currentScoringDay) =>
        scoringDay.Date <= currentScoringDay && scoringDay.Date > currentScoringDay.AddDays(-days);

    /// <param name="gameRows">The game's submissions within its window.</param>
    private LeagueGameSummaryDto Summarize(List<LeagueSubmissionRow> gameRows, DateTime currentScoringDay)
    {
        var game = gameRows[0];
        var strategy = RankingStrategyFactory.GetStrategy(game.GameRankingMode);
        var countedRows = RowsCountedForRequester(gameRows, currentScoringDay);
        var top = strategy.OrderBestFirst(countedRows).FirstOrDefault();
        var myBest = strategy.OrderBestFirst(gameRows.Where(r => r.UserId == requestingUserId && r.ScoreValue.HasValue)).FirstOrDefault();
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
            MyRank = RankOfRequester(countedRows, strategy),
            RecentPlays = recentDays.Select(day => gameRows.Count(r => r.ScoringDay.Date == day)).ToList(),
            RecentPlayDays = recentDays.Select(ScoringDayCalculator.FormatScoringDay).ToList(),
        };
    }

    /// <summary>Scored rows, without other members' current-day rows while the hidden-scores rule applies.</summary>
    private List<LeagueSubmissionRow> RowsCountedForRequester(List<LeagueSubmissionRow> gameRows, DateTime currentScoringDay)
    {
        var requesterPlayedToday = gameRows.Any(r => r.UserId == requestingUserId && r.ScoringDay == currentScoringDay);
        return gameRows
            .Where(r => r.ScoreValue.HasValue)
            .Where(r => requesterPlayedToday || r.UserId == requestingUserId || r.ScoringDay.Date != currentScoringDay)
            .ToList();
    }

    private int? RankOfRequester(List<LeagueSubmissionRow> countedRows, IRankingStrategy strategy)
    {
        var bestPerUserBestFirst = strategy.OrderBestFirst(
                countedRows.GroupBy(r => r.UserId).Select(userRows => strategy.OrderBestFirst(userRows).First()))
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
}
