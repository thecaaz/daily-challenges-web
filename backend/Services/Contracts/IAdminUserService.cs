using DailyChallenges.DTOs;

namespace DailyChallenges.Services;

public interface IAdminUserService
{
    Task<(List<UserDto> Items, int TotalCount)> GetUsersAsync(int page = 1, int pageSize = 50, string? search = null);

    /// <summary>
    /// Adjusts TotalXp in one transaction (never below 0). Throws ValidationException for a zero delta and
    /// KeyNotFoundException for a missing user.
    /// </summary>
    Task<UserDto> AdjustXpAsync(XpAdjustment adjustment);

    /// <summary>
    /// Sets a new password and ends the user's sessions. Throws ValidationException for a blank or invalid password,
    /// KeyNotFoundException for a missing user and ForbiddenException for another admin's account.
    /// </summary>
    Task SetPasswordAsync(int userId, string? newPassword, int? requestingAdminId = null);
}
