using DailyChallenges.Models;

namespace DailyChallenges.Repositories
{
    public interface IXpEventRepository
    {
        Task<(List<XpEvent> Items, int TotalCount)> GetByUserPagedAsync(int userId, int page, int pageSize, DateTime? from = null, DateTime? to = null, string? eventType = null);
    }
}
