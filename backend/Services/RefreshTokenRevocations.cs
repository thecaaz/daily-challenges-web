using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;

namespace DailyChallenges.Services;

/// <summary>The list of consumed refresh tokens, and what happens when one is presented again.</summary>
public sealed class RefreshTokenRevocations(ISessionRepository sessions, TimeProvider time, ILogger<RefreshTokenRevocations> logger)
{
    /// <summary>
    /// A rotated refresh token replayed within this window is only rejected (concurrent tabs, retried
    /// requests); replayed later it is treated as stolen and every session of the user is ended.
    /// </summary>
    public static readonly TimeSpan ReuseGracePeriod = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Records the token as used. The insert is atomic, so of two concurrent refreshes with the same
    /// token only one wins. Returns false when the token already was revoked (reuse).
    /// </summary>
    public Task<bool> TryRevokeAsync(RefreshTokenClaims token, RevokedRefreshTokenReason reason, CancellationToken ct) =>
        sessions.TryRevokeAsync(new RevokedRefreshToken
        {
            Jti = token.Jti,
            ExpiresAt = token.ExpiresAt,
            RevokedAt = time.GetUtcNow().UtcDateTime,
            Reason = reason
        }, ct);

    /// <summary>
    /// A refresh token was presented again after it was revoked. Shortly after its rotation that is normal
    /// (two tabs refreshing at once, or a lost response that the client retries), so it is just rejected.
    /// Long after its rotation, the old token can only come from a copy: someone else holds (or held) it.
    /// Since we can't tell which party is the legitimate one, every session of the user is ended by bumping
    /// TokenVersion. Logout-revoked (and legacy, undated) tokens are only rejected.
    /// It runs with <see cref="CancellationToken.None"/>: whoever replays the token must not be able to skip
    /// the theft response by aborting the request.
    /// </summary>
    public async Task HandleReuseAsync(RefreshTokenClaims token)
    {
        var revocation = await sessions.GetRevocationAsync(token.Jti, CancellationToken.None);
        var now = time.GetUtcNow().UtcDateTime;

        if (revocation is { Reason: RevokedRefreshTokenReason.Rotated, RevokedAt: { } revokedAt }
            && now - revokedAt > ReuseGracePeriod)
        {
            await EndAllSessionsAsync(token, now - revokedAt);
            return;
        }

        logger.LogWarning("Refresh rejected for user {UserId}: refresh token {Jti} was already used or revoked", token.UserId, token.Jti);
    }

    private async Task EndAllSessionsAsync(RefreshTokenClaims token, TimeSpan sinceRotation)
    {
        var ended = await sessions.EndAllSessionsAsync(token.UserId, token.TokenVersion, CancellationToken.None);
        logger.LogWarning(
            "Refresh token {Jti} of user {UserId} was replayed {Seconds:F0}s after its rotation; treating it as stolen and ending all sessions (bumped: {Bumped})",
            token.Jti, token.UserId, sinceRotation.TotalSeconds, ended);
    }
}
