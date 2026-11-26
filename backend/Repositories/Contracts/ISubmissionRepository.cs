using DailyChallenges.Models;
using DailyChallenges.Services.Ranking;

namespace DailyChallenges.Repositories;

public interface ISubmissionRepository
{
    Task<List<Submission>> GetByGameAsync(int gameId, CancellationToken ct);
    /// <summary>Best scored submissions, ordered and limited in SQL (see <see cref="TopScoresQuery"/>).</summary>
    Task<List<Submission>> GetTopByScoreValueAsync(TopScoresQuery query, IRankingStrategy strategy, CancellationToken ct);
    Task<List<Submission>> GetByGameAndDayByScoreValueAsync(int gameId, DateTime scoringDay, IRankingStrategy strategy, CancellationToken ct);
    /// <summary>True when the user has a submission for the game on the stored scoring day.</summary>
    Task<bool> ExistsForUserGameDayAsync(int userId, int gameId, DateTime scoringDay, CancellationToken ct);
    Task<List<Submission>> GetByUserAndGamesAndDaysAsync(int userId, List<int> gameIds, List<DateTime> scoringDays, CancellationToken ct);
    Task<List<Submission>> GetByScoringDaysAsync(List<DateTime> scoringDays, CancellationToken ct);
    /// <summary>
    /// Counts aggregated in SQL over each game's given scoring day: the number of games with at least one
    /// submission on that day, and the total number of those submissions.
    /// </summary>
    Task<(int ActiveGameCount, int SubmissionCount)> CountOnGameDaysAsync(IReadOnlyDictionary<int, DateTime> dayByGame, CancellationToken ct);
    /// <summary>
    /// Recent activity of the given users, limited in SQL (see <see cref="RecentActivityQuery"/>).
    /// Blob-free; ordered by game recency, then newest first within a game.
    /// </summary>
    Task<List<Submission>> GetRecentActivityByUsersAsync(RecentActivityQuery query, CancellationToken ct);
    /// <summary>One page of the game's submissions, newest first, and the total count of matching rows.</summary>
    Task<(List<Submission> Items, int TotalCount)> GetByGameFilteredAsync(SubmissionPageQuery query, CancellationToken ct);
    Task<Submission?> GetWinnerForGameAndDayAsync(int gameId, DateTime scoringDay, IRankingStrategy strategy, CancellationToken ct);
    Task<List<Submission>> GetWinnersForGameAndDaysAsync(int gameId, List<DateTime> days, IRankingStrategy strategy, CancellationToken ct);
    Task<List<DateTime>> GetAvailableDatesAsync(int gameId, CancellationToken ct);
    Task<List<string>> GetUsernamesForDayAsync(int gameId, DateTime scoringDay, CancellationToken ct);
    Task<int> CountByUserAsync(int userId, CancellationToken ct);
    /// <summary>Id, Score and ScoreValue of every submission, ordered by Id (for the score re-parse maintenance job).</summary>
    Task<List<(int Id, string Score, double? ScoreValue)>> GetScoreRowsAsync(CancellationToken ct);
    /// <summary>Sets one submission's ScoreValue in SQL (bypasses the change tracker); returns the affected row count.</summary>
    Task<int> SetScoreValueAsync(int id, double? scoreValue, CancellationToken ct);
    /// <summary>
    /// Id, UserId, CreatedAt and stored ScoringDay of the game's submissions, only those after
    /// <paramref name="afterScoringDay"/> when it is given (for re-deriving scoring days after a reset time change).
    /// </summary>
    Task<List<(int Id, int? UserId, DateTime CreatedAt, DateTime ScoringDay)>> GetScoringDayRowsAsync(int gameId, DateTime? afterScoringDay, CancellationToken ct);
    /// <summary>Sets one submission's ScoringDay in SQL (bypasses the change tracker).</summary>
    Task SetScoringDayAsync(int id, DateTime scoringDay, CancellationToken ct);
    /// <summary>The earliest stored scoring day of the game's submissions, or null when it has none.</summary>
    Task<DateTime?> GetFirstScoringDayAsync(int gameId, CancellationToken ct);
    Task<Submission> CreateAsync(Submission submission, CancellationToken ct);
    /// <summary>Tracked, full entity (including the screenshot blob). For write paths.</summary>
    Task<Submission?> GetByIdAsync(int id, CancellationToken ct);
    /// <summary>Read-only copy without the screenshot blob.</summary>
    Task<Submission?> GetByIdWithoutBlobAsync(int id, CancellationToken ct);
    /// <summary>Only the screenshot bytes and content type; (null, null) when the submission does not exist.</summary>
    Task<(byte[]? Data, string? ContentType)> GetBlobAsync(int id, CancellationToken ct);
    Task<Submission> UpdateAsync(Submission submission, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

/// <summary>
/// The best <see cref="Top"/> scored submissions of a game (10 when <see cref="Top"/> is below 1),
/// optionally only one user's, and without the rows of <see cref="ExcludeScoringDay"/>.
/// </summary>
public sealed record TopScoresQuery(int GameId, int Top)
{
    public int? UserId { get; init; }
    public DateTime? ExcludeScoringDay { get; init; }
}

/// <summary>
/// A 1-based page (a page below 1 reads as the first) of a game's submissions. <see cref="Search"/> matches
/// username or score text case-insensitively, and for an integer term also score values within 0.01 of it.
/// </summary>
public sealed record SubmissionPageQuery(int GameId, int Page, int PageSize)
{
    public string? Search { get; init; }
    public DateTime? ScoringDay { get; init; }
    public DateTime? ExcludeScoringDay { get; init; }
}

/// <summary>
/// Of the <see cref="MaxGames"/> games the <see cref="UserIds"/> most recently submitted to (since
/// <see cref="Since"/>), the latest <see cref="PerGame"/> submissions of each.
/// </summary>
public sealed record RecentActivityQuery(IReadOnlyCollection<int> UserIds, DateTime Since, int MaxGames, int PerGame);
