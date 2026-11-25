using System.Security.Claims;
using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Services;

public sealed class CurrentUserService(IUserRepository users, LevelCalculator levels, TimeProvider time) : ICurrentUserService
{
    public async Task<UserDto?> GetCurrentUserAsync(ClaimsPrincipal user)
    {
        var userId = user.GetUserId();
        if (userId == null) return null;

        var current = await users.GetByIdAsync(userId.Value);
        if (current == null) return null;

        return DtoMapper.ToDto(current, levels, time.GetUtcNow().UtcDateTime);
    }
}
