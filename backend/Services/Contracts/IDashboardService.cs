using DailyChallenges.DTOs;

namespace DailyChallenges.Services.Contracts
{
    public interface IDashboardService
    {
        Task<DashboardDataDto> GetDashboardDataAsync(int userId);
    }
}
