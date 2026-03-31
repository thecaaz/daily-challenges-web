using DailyChallenges.DTOs;
using System.Security.Claims;

namespace DailyChallenges.Services
{
    public interface ISubmissionService
    {
        Task<SubmissionPageDto> GetByGameAsync(int gameId, ClaimsPrincipal? user, DateTime? scoringDay = null, int page = 1, int pageSize = 50);
        Task<List<SubmissionDto>> GetUnfilteredByGameAsync(int gameId);
        Task<List<string>> GetAvailableDatesAsync(int gameId);
        Task<(byte[]? Data, string? ContentType)> GetScreenshotAsync(int id);
        /// <returns>Tuple of the created SubmissionDto and the XP awarded (0 for anonymous users).</returns>
        Task<(SubmissionDto Dto, int XpGain)> CreateAsync(int gameId, string score, string? username, IFormFile? screenshot, ClaimsPrincipal user);
        Task<SubmissionDto?> GetByIdAsync(int id);
        Task<SubmissionDto> UpdateAsync(int id, string? score);
        Task DeleteAsync(int id);
        Task<bool> HasUserSubmittedForLatestAsync(int gameId, ClaimsPrincipal? user);
        Task<SubmissionDto?> GetWinnerAsync(int gameId, DateTime? scoringDay = null);
        Task<TodaySubmittersDto> GetTodaySubmittersAsync(int gameId);
    }
}
