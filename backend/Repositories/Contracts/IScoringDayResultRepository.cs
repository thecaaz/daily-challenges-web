using DailyChallenges.Models;

namespace DailyChallenges.Repositories;

public interface IScoringDayResultRepository
{
    Task<bool> ExistsAsync(int gameId, DateTime scoringDay, CancellationToken ct);
    /// <summary>
    /// Inserts the result row, which claims the day for one finalizer run. Returns false, leaving the row
    /// untracked, when the day is already finalized (unique GameId/ScoringDay index); other failures throw.
    /// </summary>
    Task<bool> TryCreateAsync(ScoringDayResult result, CancellationToken ct);
    /// <summary>The tracked result row of the game and day, or null when the day is not finalized.</summary>
    Task<ScoringDayResult?> GetAsync(int gameId, DateTime scoringDay, CancellationToken ct);
    /// <summary>Deletes the row (un-finalizes the day) and flushes all pending tracked changes.</summary>
    Task DeleteAsync(ScoringDayResult result, CancellationToken ct);
    Task<int> CountWinsByUserAsync(int userId, CancellationToken ct);
    /// <summary>The latest finalized scoring day of the game, or null when none is finalized yet.</summary>
    Task<DateTime?> GetLastFinalizedDayAsync(int gameId, CancellationToken ct);
    /// <summary>The game's finalized scoring days in [<paramref name="from"/>, <paramref name="to"/>] (dates, inclusive).</summary>
    Task<List<DateTime>> GetFinalizedDaysAsync(int gameId, DateTime from, DateTime to, CancellationToken ct);
}
