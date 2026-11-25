using DailyChallenges.DTOs;
using DailyChallenges.Services.Ranking;

namespace DailyChallenges.Repositories.Contracts;

/// <summary>One game's scoring day within one league.</summary>
public sealed record LeagueGameDay(int LeagueId, int GameId, DateTime ScoringDay);

/// <summary>A member's best submission on a league leaderboard, with its standard competition rank.</summary>
public sealed record LeagueLeaderboardRow(
    int UserId,
    string Username,
    int Rank,
    string Score,
    double ScoreValue,
    int SubmissionId,
    bool HasScreenshot);

/// <summary>Read models that rank league members against each other.</summary>
public interface ILeagueStandingsRepository
{
    /// <summary>
    /// Returns the best submission per member for the given game and scoring day,
    /// ordered by the provided ranking strategy. Only members with a ScoreValue are included.
    /// </summary>
    Task<List<LeagueLeaderboardRow>> GetLeaderboardAsync(LeagueGameDay gameDay, IRankingStrategy strategy);

    /// <summary>
    /// Returns a compact summary for each game played by league members in the
    /// given recent window (days). Includes play counts, top score, the
    /// requesting user's best score and rank, and a small recent-play histogram.
    /// </summary>
    Task<(List<LeagueGameSummaryDto> Items, int TotalCount)> GetLeagueGameSummariesAsync(LeagueGameSummaryQuery query);
}
