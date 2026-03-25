using DailyChallenges.DTOs;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace DailyChallenges.Services
{
    public interface ISubmissionService
    {
        Task<SubmissionPageDto> GetByGameAsync(int gameId, ClaimsPrincipal? user, int page = 1, int pageSize = 50);
        Task<List<SubmissionDto>> GetUnfilteredByGameAsync(int gameId);
        Task<(byte[]? Data, string? ContentType)> GetScreenshotAsync(int id);
        Task<SubmissionDto> CreateAsync(int gameId, string score, string? username, IFormFile? screenshot, ClaimsPrincipal user);
        Task<SubmissionDto?> GetByIdAsync(int id);
        Task<SubmissionDto> UpdateAsync(int id, string? score);
        Task DeleteAsync(int id);
    }
}
