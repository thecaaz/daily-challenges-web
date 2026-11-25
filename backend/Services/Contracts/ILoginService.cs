using DailyChallenges.DTOs;

namespace DailyChallenges.Services.Contracts;

public interface ILoginService
{
    /// <summary>
    /// Checks the credentials and starts a session (access token in the result, refresh cookie on
    /// <paramref name="response"/>). Throws AuthenticationFailedException("Invalid credentials") for an
    /// unknown username or a wrong password alike.
    /// </summary>
    Task<AuthResultDto> LoginAsync(string username, string password, HttpResponse response);
}
