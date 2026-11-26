namespace DailyChallenges.DTOs;

/// <summary>
/// A league leaderboard request. <see cref="ScoringDay"/> null means the game's current scoring day;
/// <see cref="RequesterIsAdmin"/> lets the requester see hidden current-day scores.
/// </summary>
public sealed record LeagueLeaderboardQuery(
    int LeagueId,
    int GameId,
    DateTime? ScoringDay,
    int RequestingUserId,
    bool RequesterIsAdmin);

/// <summary>A page of per-game summaries for the last <see cref="Days"/> scoring days of a league.</summary>
public sealed record LeagueGameSummaryQuery(
    int LeagueId,
    int RequestingUserId,
    int Days,
    int Page,
    int PageSize);
