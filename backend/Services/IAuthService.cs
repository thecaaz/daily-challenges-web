using System.Security.Claims;
using DailyChallenges.DTOs;

namespace DailyChallenges.Services
{
    public interface IAuthService
    {
        Task<UserDto> RegisterAsync(string username, string password);
        Task<UserDto> LoginAsync(string username, string password, HttpResponse response);
        Task LogoutAsync(HttpResponse response);
        Task<UserDto?> GetCurrentUserAsync(ClaimsPrincipal user);
    }
}
