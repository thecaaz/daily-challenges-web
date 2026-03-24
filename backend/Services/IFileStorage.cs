using Microsoft.AspNetCore.Http;

namespace DailyChallenges.Services
{
    public interface IFileStorage
    {
        Task<(byte[]? Data, string? ContentType)> ReadFileAsync(IFormFile? file);
    }
}
