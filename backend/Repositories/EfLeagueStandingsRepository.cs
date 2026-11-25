using DailyChallenges.Data;
using DailyChallenges.DTOs;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services;
using DailyChallenges.Services.Ranking;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public sealed class EfLeagueStandingsRepository(AppDbContext db, TimeProvider time) : ILeagueStandingsRepository
{
    private const string UnknownUsername = "Unknown";
    private const int DefaultPageSize = 20;

    public async Task<List<LeagueLeaderboardRow>> GetLeaderboardAsync(LeagueGameDay gameDay, IRankingStrategy strategy)
    {
        var memberIds = await GetMemberIdsAsync(gameDay.LeagueId);
        if (memberIds.Count == 0) return [];

        var bestPerMember = await GetBestSubmissionPerMemberAsync(memberIds, gameDay, strategy);
        var ranks = CompetitionRanking.RanksOf(bestPerMember.Select(s => s.ScoreValue).ToList());
        return bestPerMember
            .Select((s, index) => new LeagueLeaderboardRow(
                s.UserId, s.Username, ranks[index], s.Score, s.ScoreValue, s.SubmissionId, s.HasScreenshot))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<(List<LeagueGameSummaryDto> Items, int TotalCount)> GetLeagueGameSummariesAsync(LeagueGameSummaryQuery query)
    {
        var memberIds = await GetMemberIdsAsync(query.LeagueId);
        if (memberIds.Count == 0) return ([], 0);

        var nowUtc = time.GetUtcNow().UtcDateTime;
        var candidateRows = await GetMemberSubmissionsAroundTodayAsync(memberIds, nowUtc, query.Days);
        var currentDayByGame = candidateRows
            .GroupBy(r => r.GameId)
            .ToDictionary(g => g.Key, g => ScoringDayHelper.GetCurrentScoringDay(g.First().GameResetTime, nowUtc).Date);
        var windowRows = candidateRows
            .Where(r => IsWithinLastScoringDays(r.ScoringDay, currentDayByGame[r.GameId], query.Days))
            .ToList();
        var playedGamesCurrentDay = currentDayByGame
            .Where(entry => windowRows.Any(r => r.GameId == entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value);
        var gamesRequesterPlayedToday = await GetGamesSubmittedOnCurrentDayAsync(query.RequestingUserId, playedGamesCurrentDay);

        var calculator = new LeagueGameSummaryCalculator(query.RequestingUserId, query.Days, gamesRequesterPlayedToday);
        var summaries = windowRows
            .GroupBy(r => r.GameId)
            .Select(game => calculator.Summarize(game.ToList(), currentDayByGame[game.Key]))
            .OrderByDescending(summary => summary.LastPlayedAt)
            .ToList();

        return (PageOf(summaries, query.Page, query.PageSize), summaries.Count);
    }

    private async Task<List<int>> GetMemberIdsAsync(int leagueId) =>
        await db.LeagueMembers
            .Where(m => m.LeagueId == leagueId)
            .Select(m => m.UserId)
            .ToListAsync();

    /// <summary>
    /// The rows come back in strategy order (best first), and GroupBy keeps the order in which keys first
    /// appear, so the first row of each member's group is their best and the groups stay in ranking order.
    /// </summary>
    private async Task<List<MemberBestSubmission>> GetBestSubmissionPerMemberAsync(
        List<int> memberIds, LeagueGameDay gameDay, IRankingStrategy strategy)
    {
        var targetDay = gameDay.ScoringDay.Date;
        var scoredSubmissions = db.Submissions
            .Where(s =>
                s.GameId == gameDay.GameId &&
                s.ScoringDay == targetDay &&
                s.UserId.HasValue &&
                memberIds.Contains(s.UserId!.Value) &&
                s.ScoreValue.HasValue);

        var submissionsBestFirst = await strategy.ApplyOrdering(scoredSubmissions)
            .Select(s => new
            {
                s.Id,
                s.UserId,
                s.Score,
                s.ScoreValue,
                Username = s.Username != null ? s.Username : s.User != null ? s.User.Username : UnknownUsername,
                HasScreenshot = s.ScreenshotContentType != null && s.ScreenshotContentType != ""
            })
            .ToListAsync();

        return submissionsBestFirst
            .GroupBy(s => s.UserId!.Value)
            .Select(memberSubmissions => memberSubmissions.First())
            .Select(s => new MemberBestSubmission(
                s.UserId!.Value, s.Username ?? UnknownUsername, s.Score, s.ScoreValue!.Value, s.Id, s.HasScreenshot))
            .ToList();
    }

    /// <summary>
    /// A game's current scoring day is within ±1 of the UTC date (late resets label "today" with tomorrow's
    /// date), so this loads the widest possible range; callers narrow it per game. Only the needed columns
    /// are projected, which keeps screenshot blobs out of the query.
    /// </summary>
    private async Task<List<LeagueSubmissionRow>> GetMemberSubmissionsAroundTodayAsync(List<int> memberIds, DateTime nowUtc, int days)
    {
        var rangeStart = nowUtc.Date.AddDays(-days);
        var rangeEnd = nowUtc.Date.AddDays(1);

        return await db.Submissions
            .Where(s => s.UserId.HasValue && memberIds.Contains(s.UserId!.Value)
                        && s.ScoringDay >= rangeStart && s.ScoringDay <= rangeEnd)
            .Select(s => new LeagueSubmissionRow(
                s.GameId,
                s.Game!.Name,
                s.Game.ScreenshotContentType != null && s.Game.ScreenshotContentType != "",
                s.Game!.RankingMode,
                s.Game!.ResetTime,
                s.UserId!.Value,
                s.Score,
                s.ScoreValue,
                s.ScoringDay,
                s.CreatedAt))
            .ToListAsync();
    }

    private static bool IsWithinLastScoringDays(DateTime scoringDay, DateTime currentScoringDay, int days) =>
        scoringDay.Date <= currentScoringDay && scoringDay.Date > currentScoringDay.AddDays(-days);

    private async Task<HashSet<int>> GetGamesSubmittedOnCurrentDayAsync(int userId, Dictionary<int, DateTime> currentDayByGame)
    {
        if (currentDayByGame.Count == 0) return [];

        var gameIds = currentDayByGame.Keys.ToList();
        var currentDays = currentDayByGame.Values.Distinct().ToList();
        var userSubmissions = await db.Submissions
            .Where(s => s.UserId == userId && gameIds.Contains(s.GameId) && currentDays.Contains(s.ScoringDay))
            .Select(s => new { s.GameId, s.ScoringDay })
            .ToListAsync();

        return userSubmissions
            .Where(s => currentDayByGame[s.GameId] == s.ScoringDay.Date)
            .Select(s => s.GameId)
            .ToHashSet();
    }

    private static List<LeagueGameSummaryDto> PageOf(List<LeagueGameSummaryDto> summaries, int page, int pageSize)
    {
        var size = pageSize < 1 ? DefaultPageSize : pageSize;
        var number = Math.Max(1, page);
        return summaries.Skip((number - 1) * size).Take(size).ToList();
    }

    private sealed record MemberBestSubmission(
        int UserId, string Username, string Score, double ScoreValue, int SubmissionId, bool HasScreenshot);
}
