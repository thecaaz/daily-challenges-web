using DailyChallenges.DTOs;

namespace DailyChallenges.Services.Contracts;

public interface IRegistrationService
{
    /// <summary>
    /// Creates an account (the first one ever becomes admin) after <see cref="CredentialRules"/> validation.
    /// Throws ValidationException for invalid credentials and ConflictException when the username is taken
    /// (case-insensitively).
    /// </summary>
    Task<UserDto> RegisterAsync(string username, string password, CancellationToken ct);
}
