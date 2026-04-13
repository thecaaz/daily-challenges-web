using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using DailyChallenges.DTOs;

namespace DailyChallenges.Services
{
    public interface IGameService
    {
        Task<List<GameDto>> GetAllAsync(ClaimsPrincipal? user = null);
        Task<GameDto> CreateAsync(string name, IFormFile? image, string? resetTime, string? url, string? description, int? resetTimezoneOffsetMinutes = null, string? rankingMode = null);
        Task<Models.Game?> GetByIdAsync(int id);
        Task<GameDto> UpdateAsync(int id, string? name, IFormFile? image, string? resetTime, string? url, string? description, int? resetTimezoneOffsetMinutes = null, string? rankingMode = null);
        Task DeleteAsync(int id);
        Task<(byte[]? Data, string? ContentType)> GetImageAsync(int id);
        Task<HighscoreResult> GetHighscoreAsync(int id, ClaimsPrincipal? user);
        Task<HighscoreResult> GetPersonalHighscoreAsync(int id, int userId);
        Task<GameOverviewDto> GetOverviewAsync(int gameId, ClaimsPrincipal? user, string? include = null, int top = 0);
    }

    public class HighscoreResult
    {
        public SubmissionDto? Highscore { get; set; }
        public List<SubmissionDto> Top { get; set; } = new();
    }
}
