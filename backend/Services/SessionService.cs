using DailyChallenges.DTOs;
using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Services;

public sealed class SessionService(
    IUserRepository users,
    RefreshTokenCookie cookie,
    RefreshTokenReader reader,
    RefreshTokenRevocations revocations,
    SessionIssuer sessions,
    ILogger<SessionService> logger) : ISessionService
{
    /// <summary>Rotation: the presented token is consumed; a replayed (stolen or old) token is rejected.</summary>
    public async Task<AuthResultDto?> RefreshAsync(HttpRequest request, HttpResponse response, CancellationToken ct)
    {
        var token = ReadPresentedToken(request);
        if (token == null) return null;

        var user = await users.GetByIdAsync(token.UserId);
        if (user == null) return null;
        if (IsFromOlderTokenVersion(token, user)) return null;

        if (!await revocations.TryRevokeAsync(token, RevokedRefreshTokenReason.Rotated))
        {
            await revocations.HandleReuseAsync(token);
            return null;
        }

        return sessions.Issue(user, response);
    }

    public async Task LogoutAsync(HttpRequest request, HttpResponse response, CancellationToken ct)
    {
        var token = ReadPresentedToken(request);
        if (token != null)
            await revocations.TryRevokeAsync(token, RevokedRefreshTokenReason.Logout);

        cookie.Delete(response);
    }

    private RefreshTokenClaims? ReadPresentedToken(HttpRequest request) =>
        cookie.TryRead(request, out var refreshToken) ? reader.Read(refreshToken) : null;

    /// <summary>A password reset (or delete) bumps TokenVersion, which ends every older session.</summary>
    private bool IsFromOlderTokenVersion(RefreshTokenClaims token, User user)
    {
        if (user.TokenVersion == token.TokenVersion) return false;

        logger.LogInformation("Refresh rejected for user {UserId}: token version {TokenVersion} is outdated", token.UserId, token.TokenVersion);
        return true;
    }
}
