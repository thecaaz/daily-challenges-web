using DailyChallenges.Models;

namespace DailyChallenges.Repositories;

public interface IXpEventRepository
{
    /// <summary>Newest first; a page below 1 reads as the first page (see <see cref="XpEventFilter"/>).</summary>
    Task<(List<XpEvent> Items, int TotalCount)> GetByUserPagedAsync(XpEventFilter filter, int page, int pageSize);
    /// <summary>
    /// Sum of the user's XP events of the given types whose (GameId, ScoringDay) matches a pair in
    /// <paramref name="dayByGame"/> (e.g. each game's current scoring day).
    /// </summary>
    Task<int> SumAmountByUserAndGameDaysAsync(int userId, IReadOnlyDictionary<int, DateTime> dayByGame, IReadOnlyCollection<string> eventTypes);
    /// <summary>True when a "day_win" event already exists for the user, game and scoring day.</summary>
    Task<bool> HasDayWinAsync(int userId, int gameId, DateTime scoringDay);
    /// <summary>The tracked "day_win" event of the user, game and scoring day, or null.</summary>
    Task<XpEvent?> GetDayWinAsync(int userId, int gameId, DateTime scoringDay);
    /// <summary>Deletes the event and flushes all pending tracked changes.</summary>
    Task RemoveAsync(XpEvent xpEvent);
    /// <summary>Sets ScoringDay of the submission's XP events in SQL (bypasses the change tracker).</summary>
    Task SetScoringDayForSubmissionAsync(int submissionId, DateTime scoringDay);
    /// <summary>Persists a new XpEvent and flushes all pending tracked changes.</summary>
    Task<XpEvent> AddAsync(XpEvent xpEvent);
}

/// <summary>
/// A user's XP events, optionally only those created in [<see cref="From"/>, <see cref="To"/>) and of
/// <see cref="EventType"/> (a blank type does not filter).
/// </summary>
public sealed record XpEventFilter(int UserId)
{
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string? EventType { get; init; }
}
