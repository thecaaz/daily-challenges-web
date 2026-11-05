using System.ComponentModel.DataAnnotations;

namespace DailyChallenges.Models
{
    /// <summary>
    /// A refresh token (by its "jti") that was rotated or logged out and must not be accepted again.
    /// Rows are purged once the token would have expired anyway.
    /// </summary>
    public class RevokedRefreshToken
    {
        [Key]
        [MaxLength(64)]
        public string Jti { get; set; } = string.Empty;

        /// <summary>UTC expiry of the revoked token; after it the row is no longer needed.</summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// UTC time the token was revoked. Null for rows written before this was recorded; those are
        /// treated like a logout (a replay is rejected without further action).
        /// </summary>
        public DateTime? RevokedAt { get; set; }

        /// <summary>Why the token was revoked; decides whether a later replay counts as token theft.</summary>
        public RevokedRefreshTokenReason Reason { get; set; } = RevokedRefreshTokenReason.Rotated;
    }

    public enum RevokedRefreshTokenReason
    {
        /// <summary>Consumed by a successful refresh (replaced by a new token).</summary>
        Rotated = 0,

        /// <summary>Revoked by logging out on that device.</summary>
        Logout = 1
    }
}
