using DailyChallenges.Data;
using DailyChallenges.DTOs;
using DailyChallenges.Exceptions;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Services;

public sealed class RegistrationService(
    IUserRepository users,
    ITransactionRunner transactions,
    LevelCalculator levels,
    TimeProvider time) : IRegistrationService
{
    private const string UsernameTakenMessage = "Username already taken";

    /// <summary>The password is hashed before the transaction starts, so the write lock isn't held for the BCrypt work.</summary>
    public async Task<UserDto> RegisterAsync(string username, string password, CancellationToken ct)
    {
        var name = CredentialRules.ValidateUsername(username);
        CredentialRules.ValidatePassword(password);
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

        var user = await CreateUserAsync(name, passwordHash);

        return DtoMapper.ToDto(user, levels, time.GetUtcNow().UtcDateTime);
    }

    /// <summary>
    /// The existence check, the first-user-is-admin check and the insert run in one BEGIN IMMEDIATE
    /// transaction, serialized with other registrations, so two concurrent first registrations can't
    /// both become admin. The NOCASE unique index on Username is the last line of defence.
    /// </summary>
    private async Task<User> CreateUserAsync(string username, string passwordHash)
    {
        try
        {
            return await transactions.RunAsync(() => InsertNewUserAsync(username, passwordHash));
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            throw new ConflictException(UsernameTakenMessage);
        }
    }

    private async Task<User> InsertNewUserAsync(string username, string passwordHash)
    {
        if (await users.ExistsWithUsernameAsync(username))
            throw new ConflictException(UsernameTakenMessage);

        var isFirstUser = await users.IsEmptyAsync();
        return await users.CreateAsync(new User { Username = username, PasswordHash = passwordHash, IsAdmin = isFirstUser });
    }
}
