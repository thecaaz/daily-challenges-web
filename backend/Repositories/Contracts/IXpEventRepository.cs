using DailyChallenges.Models;

namespace DailyChallenges.Repositories
{
    public interface IXpEventRepository
    {
        Task<(List<XpEvent> Items, int TotalCount)> GetByUserPagedAsync(int userId, int page, int pageSize, DateTime? from = null, DateTime? to = null, string? eventType = null);
        Task<int> SumAmountByUserAndDaysAndTypesAsync(int userId, List<DateTime> scoringDays, string[] eventTypes);
        /// <summary>Persists a new XpEvent and flushes all pending tracked changes.</summary>
        Task<XpEvent> AddAsync(XpEvent xpEvent);
    }
}
