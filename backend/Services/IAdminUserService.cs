using DailyChallenges.DTOs;

namespace DailyChallenges.Services
{
    public interface IAdminUserService
    {
        Task<(List<UserDto> Items, int TotalCount)> GetUsersAsync(int page = 1, int pageSize = 50, string? search = null);

        Task<UserDto> AdjustXpAsync(int userId, int delta, string? reason, int? adminUserId = null);
    }
}
