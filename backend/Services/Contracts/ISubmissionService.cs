using DailyChallenges.DTOs;
using System.Security.Claims;

namespace DailyChallenges.Services
{
    public interface ISubmissionService
    {
        Task<SubmissionPageDto> GetByGameAsync(int gameId, ClaimsPrincipal? user, DateTime? scoringDay = null, int page = 1, int pageSize = 50);
        Task<List<SubmissionDto>> GetUnfilteredByGameAsync(int gameId);
        Task<List<string>> GetAvailableDatesAsync(int gameId);
        /// <summary>Current-day screenshots need a valid <paramref name="exp"/>/<paramref name="sig"/> pair (ForbiddenException otherwise).</summary>
        Task<(byte[]? Data, string? ContentType)> GetScreenshotAsync(int id, long? exp, string? sig);
        /// <returns>Tuple of the created SubmissionDto and the XP awarded (0 for anonymous users).</returns>
        Task<(SubmissionDto Dto, int XpGain)> CreateAsync(int gameId, string score, string? username, IFormFile? screenshot, ClaimsPrincipal user);
        /// <summary>Masked when the submission is on the current scoring day and the caller (not the owner) may not see it.</summary>
        Task<SubmissionDto?> GetByIdAsync(int id, ClaimsPrincipal? user);
        Task<SubmissionDto> UpdateAsync(int id, string? score);
        Task DeleteAsync(int id);
        Task<bool> HasUserSubmittedForLatestAsync(int gameId, ClaimsPrincipal? user);
        /// <summary>Without a day: the previous scoring day's winner. Null when there is none or the day is hidden.</summary>
        Task<SubmissionDto?> GetWinnerAsync(int gameId, DateTime? scoringDay, ClaimsPrincipal? user);
        Task<TodaySubmittersDto> GetTodaySubmittersAsync(int gameId);
    }
}
