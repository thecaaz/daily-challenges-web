using DailyChallenges.DTOs;
using Microsoft.AspNetCore.Http;

namespace DailyChallenges.Services
{
    public interface IGameService
    {
        Task<List<GameDto>> GetAllAsync();
        Task<GameDto> CreateAsync(string name, IFormFile? image, string? resetTime, string? resetTimezoneId, string? url);
        Task<Models.Game?> GetByIdAsync(int id);
        Task<GameDto> UpdateAsync(int id, string? name, IFormFile? image, string? resetTime, string? resetTimezoneId, string? url);
        Task DeleteAsync(int id);
        Task<HighscoreResult> GetHighscoreAsync(int id, System.Security.Claims.ClaimsPrincipal? user);
        Task<HighscoreResult> GetPersonalHighscoreAsync(int id, int userId);
    }

    public class HighscoreResult
    {
        public SubmissionDto? Highscore { get; set; }
        public List<SubmissionDto> Top { get; set; } = new();
    }
}
