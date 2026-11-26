using DailyChallenges.DTOs;

namespace DailyChallenges.Services.Contracts;

/// <summary>How league members rank against each other. Members only.</summary>
public interface ILeagueStandingsService
{
    /// <summary>
    /// <see cref="LeagueLeaderboardQuery.ScoringDay"/> defaults to the game's current scoring day. Entries are masked
    /// (ScoresHidden) on the current day unless the requester submitted for it or is an admin.
    /// </summary>
    Task<LeagueLeaderboardDto> GetLeaderboardAsync(LeagueLeaderboardQuery query, CancellationToken ct);

    /// <summary>Days are clamped to 1–31 and paging to <see cref="Paging"/> limits.</summary>
    Task<(List<LeagueGameSummaryDto> Items, int TotalCount)> GetLeagueGameSummariesAsync(LeagueGameSummaryQuery query, CancellationToken ct);
}
