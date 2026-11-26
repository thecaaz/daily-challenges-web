using DailyChallenges.DTOs;

namespace DailyChallenges.Services;

public interface IAdminUserService
{
    Task<(List<UserDto> Items, int TotalCount)> GetUsersAsync(int page, int pageSize, string? search, CancellationToken ct);

    /// <summary>
    /// Adjusts TotalXp in one transaction (never below 0). Throws ValidationException for a zero delta and
    /// NotFoundException for a missing user.
    /// </summary>
    Task<UserDto> AdjustXpAsync(XpAdjustment adjustment, CancellationToken ct);

    /// <summary>
    /// Sets a new password and ends the user's sessions. Throws ValidationException for a blank or invalid password,
    /// NotFoundException for a missing user and ForbiddenException for another admin's account.
    /// </summary>
    Task SetPasswordAsync(int userId, string? newPassword, int? requestingAdminId, CancellationToken ct);
}
