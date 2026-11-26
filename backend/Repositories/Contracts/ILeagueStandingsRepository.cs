using DailyChallenges.Models;
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

/// <summary>A league member's submission with the game fields summaries need, projected without blobs.</summary>
public sealed record LeagueSubmissionRow(
    int GameId,
    string GameName,
    bool GameHasIcon,
    RankingMode GameRankingMode,
    TimeSpan GameResetTime,
    int UserId,
    string Score,
    double? ScoreValue,
    DateTime ScoringDay,
    DateTime CreatedAt) : IRankable;

/// <summary>Read models that rank league members against each other.</summary>
public interface ILeagueStandingsRepository
{
    /// <summary>
    /// Returns the best submission per member for the given game and scoring day,
    /// ordered by the provided ranking strategy. Only members with a ScoreValue are included.
    /// </summary>
    Task<List<LeagueLeaderboardRow>> GetLeaderboardAsync(LeagueGameDay gameDay, IRankingStrategy strategy);

    /// <summary>
    /// Returns every submission of the league's members whose scoring day lies between
    /// <paramref name="firstScoringDay"/> and <paramref name="lastScoringDay"/> (both inclusive), scored or not.
    /// </summary>
    Task<List<LeagueSubmissionRow>> GetMemberSubmissionsAsync(int leagueId, DateTime firstScoringDay, DateTime lastScoringDay);
}
