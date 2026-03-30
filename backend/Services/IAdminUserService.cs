using System;
using DailyChallenges.DTOs;

namespace DailyChallenges.Services
{
    public interface IAdminUserService
    {
        Task<(List<UserDto> Items, int TotalCount)> GetUsersAsync(int page = 1, int pageSize = 50, string? search = null);

        Task<UserDto> AdjustXpAsync(int userId, int delta, string? reason, int? adminUserId = null);

        Task<(List<DailyChallenges.DTOs.XpEventDto> Items, int TotalCount)> GetXpEventsAsync(int userId, int page = 1, int pageSize = 20, DateTime? from = null, DateTime? to = null, string? eventType = null);

        Task<(byte[] Data, string Filename)> ExportXpEventsCsvAsync(int userId, DateTime? from = null, DateTime? to = null, string? eventType = null, int maxRows = 10000);
    }
}
