using DailyChallenges.DTOs;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace DailyChallenges.Services
{
    public interface ISubmissionService
    {
        Task<List<SubmissionDto>> GetByGameAsync(int gameId, System.Security.Claims.ClaimsPrincipal? user);
        Task<(byte[]? Data, string? ContentType)> GetScreenshotAsync(int id);
        Task<SubmissionDto> CreateAsync(int gameId, string score, string? username, IFormFile? screenshot, ClaimsPrincipal user);
        Task<SubmissionDto?> GetByIdAsync(int id);
        Task<SubmissionDto> UpdateAsync(int id, string? score);
        Task DeleteAsync(int id);
    }
}
