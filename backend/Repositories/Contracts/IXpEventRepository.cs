using DailyChallenges.Models;

namespace DailyChallenges.Repositories
{
    public interface IXpEventRepository
    {
        Task<(List<XpEvent> Items, int TotalCount)> GetByUserPagedAsync(int userId, int page, int pageSize, DateTime? from = null, DateTime? to = null, string? eventType = null);
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
        /// <summary>Persists a new XpEvent and flushes all pending tracked changes.</summary>
        Task<XpEvent> AddAsync(XpEvent xpEvent);
    }
}
