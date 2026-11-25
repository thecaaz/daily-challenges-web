using System.Security.Claims;
using DailyChallenges.DTOs;

namespace DailyChallenges.Services.Contracts;

public interface ICurrentUserService
{
    /// <summary>The authenticated caller, or null when unauthenticated or the user no longer exists.</summary>
    Task<UserDto?> GetCurrentUserAsync(ClaimsPrincipal user);
}
