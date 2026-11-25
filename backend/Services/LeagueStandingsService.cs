using DailyChallenges.DTOs;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Contracts;
using DailyChallenges.Services.Ranking;

namespace DailyChallenges.Services;

public sealed class LeagueStandingsService(
    ILeagueRepository leagues,
    ILeagueStandingsRepository standings,
    IGameRepository games,
    IScoreVisibilityService visibility,
    IScreenshotUrlSigner signer) : ILeagueStandingsService
{
    private const int MinSummaryDays = 1;
    private const int MaxSummaryDays = 31;

    public async Task<LeagueLeaderboardDto> GetLeaderboardAsync(LeagueLeaderboardQuery query)
    {
        await leagues.RequireMembershipAsync(query.LeagueId, query.RequestingUserId);
        var game = await games.GetByIdWithoutBlobAsync(query.GameId)
            ?? throw new KeyNotFoundException("Game not found.");

        var currentDay = visibility.GetCurrentScoringDay(game);
        var day = query.ScoringDay?.Date ?? currentDay;
        var canSeeScores = await visibility.CanSeeDayAsync(new ScoreViewer(query.RequestingUserId, query.RequesterIsAdmin), game, day);
        var rows = await standings.GetLeaderboardAsync(
            new LeagueGameDay(query.LeagueId, query.GameId, day),
            RankingStrategyFactory.GetStrategy(game.RankingMode));

        return new LeagueLeaderboardDto
        {
            LeagueId = query.LeagueId,
            GameId = query.GameId,
            ScoringDay = ScoringDayCalculator.FormatScoringDay(day),
            ScoresHidden = !canSeeScores,
            Entries = canSeeScores
                ? RankedEntries(rows, ScreenshotUrlFor(day, currentDay))
                : ParticipantsOnly(rows)
        };
    }

    public async Task<(List<LeagueGameSummaryDto> Items, int TotalCount)> GetLeagueGameSummariesAsync(LeagueGameSummaryQuery query)
    {
        await leagues.RequireMembershipAsync(query.LeagueId, query.RequestingUserId);
        return await standings.GetLeagueGameSummariesAsync(WithinLimits(query));
    }

    /// <summary>Past days' screenshots are public; current-day ones need a short-lived signed URL.</summary>
    private Func<int, string> ScreenshotUrlFor(DateTime day, DateTime currentDay) =>
        day == currentDay ? signer.SignedUrl : signer.UnsignedUrl;

    private static List<LeagueLeaderboardEntryDto> RankedEntries(
        List<LeagueLeaderboardRow> rows, Func<int, string> screenshotUrl) =>
        rows.Select(row => new LeagueLeaderboardEntryDto
        {
            Rank = row.Rank,
            UserId = row.UserId,
            Username = row.Username,
            Score = row.Score,
            ScoreValue = row.ScoreValue,
            SubmissionId = row.SubmissionId,
            ScreenshotUrl = row.HasScreenshot ? screenshotUrl(row.SubmissionId) : null
        }).ToList();

    /// <summary>While scores are hidden, who played stays visible, but not the score, rank, or order.</summary>
    private static List<LeagueLeaderboardEntryDto> ParticipantsOnly(List<LeagueLeaderboardRow> rows) =>
        rows
            .OrderBy(row => row.Username, StringComparer.OrdinalIgnoreCase)
            .Select(row => new LeagueLeaderboardEntryDto { UserId = row.UserId, Username = row.Username })
            .ToList();

    private static LeagueGameSummaryQuery WithinLimits(LeagueGameSummaryQuery query)
    {
        var (page, pageSize) = Paging.Clamp(query.Page, query.PageSize);
        return query with { Days = Math.Clamp(query.Days, MinSummaryDays, MaxSummaryDays), Page = page, PageSize = pageSize };
    }
}
