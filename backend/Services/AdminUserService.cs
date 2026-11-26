using DailyChallenges.Achievements;
using DailyChallenges.DTOs;
using DailyChallenges.Exceptions;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Services;

public sealed class AdminUserService(
    IUserRepository users,
    IXpService xp,
    LevelCalculator levels,
    ITransactionRunner transactions,
    IAchievementService achievements,
    TimeProvider time,
    ILogger<AdminUserService> logger) : IAdminUserService
{
    private const string DefaultXpAdjustmentReason = "admin_adjustment";
    private const string ZeroDeltaMessage = "Delta must be non-zero";
    private const string PasswordRequiredMessage = "NewPassword is required";

    private DateTime TodayUtc => time.GetUtcNow().UtcDateTime.Date;

    public async Task<(List<UserDto> Items, int TotalCount)> GetUsersAsync(int page, int pageSize, string? search, CancellationToken ct)
    {
        var (pageUsers, total) = await users.GetPagedAsync(page, pageSize, search, ct);
        var items = pageUsers.Select(u => DtoMapper.ToDto(u, levels, TodayUtc)).ToList();
        return (items, total);
    }

    /// <summary>
    /// The read-modify-write of TotalXp runs inside BEGIN IMMEDIATE, with the user loaded inside the
    /// transaction, so a concurrent submission or day win cannot overwrite it.
    /// </summary>
    public async Task<UserDto> AdjustXpAsync(XpAdjustment adjustment, CancellationToken ct)
    {
        if (adjustment.Delta == 0) throw new ValidationException(ZeroDeltaMessage);

        var effective = adjustment with { Reason = adjustment.Reason ?? DefaultXpAdjustmentReason };
        await transactions.RunAsync(() => xp.AdjustXpAsync(effective, ct), ct);

        logger.LogInformation("Admin {AdminId} adjusted XP of user {UserId} by {Delta} ({Reason})",
            effective.AdminUserId, effective.UserId, effective.Delta, effective.Reason);

        await achievements.CheckAndAwardAsync(effective.UserId, AchievementTrigger.LevelUp);

        var user = await GetExistingUserAsync(effective.UserId, ct);
        return DtoMapper.ToDto(user, levels, TodayUtc);
    }

    /// <summary>Bumping TokenVersion ends every existing session of the user (refresh tokens carry the old version).</summary>
    public async Task SetPasswordAsync(int userId, string? newPassword, int? requestingAdminId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(newPassword)) throw new ValidationException(PasswordRequiredMessage);
        CredentialRules.ValidatePassword(newPassword);

        var user = await GetExistingUserAsync(userId, ct);
        if (user.IsAdmin && userId != requestingAdminId)
            throw new ForbiddenException("Cannot reset another admin's password");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.TokenVersion++;
        await users.UpdateAsync(user, ct);

        logger.LogInformation("Admin {AdminId} reset the password of user {UserId}", requestingAdminId, userId);
    }

    private async Task<User> GetExistingUserAsync(int userId, CancellationToken ct) =>
        await users.GetByIdAsync(userId, ct) ?? throw new NotFoundException($"User {userId} not found");
}
