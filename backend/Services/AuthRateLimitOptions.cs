namespace DailyChallenges.Services
{
    /// <summary>
    /// Fixed-window, per-client-IP limit for /api/auth/login and /api/auth/register (config section "RateLimiting:Auth").
    /// </summary>
    public class AuthRateLimitOptions
    {
        public const string SectionName = "RateLimiting:Auth";
        public const string PolicyName = "auth";

        /// <summary>When null: enabled everywhere except the "Testing" environment.</summary>
        public bool? Enabled { get; set; }

        /// <summary>Requests allowed per client IP per window.</summary>
        public int PermitLimit { get; set; } = 10;

        public int WindowSeconds { get; set; } = 60;
    }
}
