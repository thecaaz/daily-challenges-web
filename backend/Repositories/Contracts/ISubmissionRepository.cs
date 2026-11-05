using DailyChallenges.Models;
using DailyChallenges.Services.Ranking;

namespace DailyChallenges.Repositories
{
    public interface ISubmissionRepository
    {
        Task<List<Submission>> GetByGameAsync(int gameId);
        Task<List<Submission>> GetByGamePagedAsync(int gameId, int page, int pageSize);
        /// <summary>Best scored submissions, ordered and limited in SQL. Rows of <paramref name="excludeScoringDay"/> are left out.</summary>
        Task<List<Submission>> GetTopByGameByScoreValueAsync(int gameId, int top, IRankingStrategy strategy, DateTime? excludeScoringDay = null);
        Task<List<Submission>> GetTopByGameByUserByScoreValueAsync(int gameId, int userId, int top, IRankingStrategy strategy);
        Task<List<Submission>> GetByGameAndDayByScoreValueAsync(int gameId, DateTime scoringDay, IRankingStrategy strategy);
        Task<Submission?> GetByGameAndUserAsync(int gameId, int userId);
        /// <summary>True when the user has a submission for the game on the stored scoring day.</summary>
        Task<bool> ExistsForUserGameDayAsync(int userId, int gameId, DateTime scoringDay);
        Task<List<Submission>> GetByUserAndGamesAndDaysAsync(int userId, List<int> gameIds, List<DateTime> scoringDays);
        Task<List<Submission>> GetByScoringDaysAsync(List<DateTime> scoringDays);
        /// <summary>
        /// Counts aggregated in SQL over each game's given scoring day: the number of games with at least one
        /// submission on that day, and the total number of those submissions.
        /// </summary>
        Task<(int ActiveGameCount, int SubmissionCount)> CountOnGameDaysAsync(IReadOnlyDictionary<int, DateTime> dayByGame);
        /// <summary>
        /// Recent activity of the given users, limited in SQL: of the <paramref name="maxGames"/> games they most
        /// recently submitted to (since <paramref name="since"/>), the latest <paramref name="perGame"/> submissions
        /// of each. Blob-free; ordered by game recency, then newest first within a game.
        /// </summary>
        Task<List<Submission>> GetRecentActivityByUsersAsync(IReadOnlyCollection<int> userIds, DateTime since, int maxGames, int perGame);
        Task<(List<Submission> Items, int TotalCount)> GetByGameFilteredAsync(int gameId, int page, int pageSize, string? search, DateTime? scoringDay, DateTime? excludeScoringDay = null);
        Task<Submission?> GetWinnerForGameAndDayAsync(int gameId, DateTime scoringDay, IRankingStrategy strategy);
        Task<List<Submission>> GetWinnersForGameAndDaysAsync(int gameId, List<DateTime> days, IRankingStrategy strategy);
        Task<List<DateTime>> GetAvailableDatesAsync(int gameId);
        Task<List<string>> GetUsernamesForDayAsync(int gameId, DateTime scoringDay);
        Task<int> CountByUserAsync(int userId);
        /// <summary>Id, Score and ScoreValue of every submission, ordered by Id (for the score re-parse maintenance job).</summary>
        Task<List<(int Id, string Score, double? ScoreValue)>> GetScoreRowsAsync();
        /// <summary>Sets one submission's ScoreValue in SQL (bypasses the change tracker); returns the affected row count.</summary>
        Task<int> SetScoreValueAsync(int id, double? scoreValue);
        /// <summary>The earliest stored scoring day of the game's submissions, or null when it has none.</summary>
        Task<DateTime?> GetFirstScoringDayAsync(int gameId);
        Task<Submission> CreateAsync(Submission submission);
        /// <summary>Tracked, full entity (including the screenshot blob). For write paths.</summary>
        Task<Submission?> GetByIdAsync(int id);
        /// <summary>Read-only copy without the screenshot blob.</summary>
        Task<Submission?> GetByIdWithoutBlobAsync(int id);
        /// <summary>Only the screenshot bytes and content type; (null, null) when the submission does not exist.</summary>
        Task<(byte[]? Data, string? ContentType)> GetBlobAsync(int id);
        Task<Submission> UpdateAsync(Submission submission);
        Task DeleteAsync(int id);
    }
}
