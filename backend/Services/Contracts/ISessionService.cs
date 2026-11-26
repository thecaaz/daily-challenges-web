using DailyChallenges.DTOs;

namespace DailyChallenges.Services.Contracts;

/// <summary>Refresh-cookie sessions: rotation on refresh and revocation on logout.</summary>
public interface ISessionService
{
    /// <summary>
    /// Consumes the refresh cookie and issues a new access token and refresh cookie. Null when the cookie
    /// is missing, invalid, already used, or older than the user's last TokenVersion bump.
    /// </summary>
    Task<AuthResultDto?> RefreshAsync(HttpRequest request, HttpResponse response, CancellationToken ct);

    /// <summary>Revokes this device's refresh token (other sessions keep working) and deletes the cookie.</summary>
    Task LogoutAsync(HttpRequest request, HttpResponse response, CancellationToken ct);
}
