using DailyChallenges.Models;

namespace DailyChallenges.Repositories.Contracts;

/// <summary>Persistence for refresh-token sessions: the revocation list and the per-user TokenVersion.</summary>
public interface ISessionRepository
{
    /// <summary>
    /// Inserts the revocation unless its jti is already revoked (INSERT OR IGNORE, atomic).
    /// Returns true when this call inserted it.
    /// </summary>
    Task<bool> TryRevokeAsync(RevokedRefreshToken revocation);

    /// <summary>The revocation of <paramref name="jti"/>, or null when it was never revoked (or was purged).</summary>
    Task<RevokedRefreshToken?> GetRevocationAsync(string jti);

    /// <summary>
    /// Increments the user's TokenVersion, which invalidates every token issued before, but only while it
    /// still equals <paramref name="tokenVersion"/>, so concurrent callers bump it once. True when bumped.
    /// </summary>
    Task<bool> EndAllSessionsAsync(int userId, int tokenVersion);
}
