using DailyChallenges.DTOs;
using DailyChallenges.Exceptions;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Services;

public sealed class LoginService(IUserRepository users, SessionIssuer sessions) : ILoginService
{
    private const string InvalidCredentialsMessage = "Invalid credentials";
    private const string TimingDummyPassword = "dummy-password-for-timing";

    /// <summary>
    /// Verified against when the username doesn't exist: same BCrypt cost as a real check, so the
    /// response time doesn't reveal unknown usernames.
    /// </summary>
    private static readonly Lazy<string> UnknownUserPasswordHash = new(() => BCrypt.Net.BCrypt.HashPassword(TimingDummyPassword));

    /// <summary>Username lookups are case-insensitive (NOCASE column).</summary>
    public async Task<AuthResultDto> LoginAsync(string username, string password, HttpResponse response, CancellationToken ct)
    {
        var user = await users.GetByUsernameAsync(CredentialRules.NormalizeUsername(username), ct);
        if (user == null)
        {
            BCrypt.Net.BCrypt.Verify(password ?? string.Empty, UnknownUserPasswordHash.Value);
            throw new AuthenticationFailedException(InvalidCredentialsMessage);
        }

        if (!BCrypt.Net.BCrypt.Verify(password ?? string.Empty, user.PasswordHash))
            throw new AuthenticationFailedException(InvalidCredentialsMessage);

        return sessions.Issue(user, response);
    }
}
