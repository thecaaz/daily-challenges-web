using System.ComponentModel.DataAnnotations;

namespace DailyChallenges.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// Embedded as the "tv" claim in refresh tokens. Bumping it (password reset, delete) invalidates
        /// every refresh token issued before, i.e. logs the user out on all devices.
        /// </summary>
        public int TokenVersion { get; set; } = 0;

        // ── XP / Level ──────────────────────────────────────────────────────────
        /// <summary>Lifetime XP from every source (submissions, day wins, admin adjustments); never below 0.</summary>
        public long TotalXp { get; set; } = 0;

        /// <summary>Current level, derived from TotalXp and kept in sync on every award.</summary>
        public int Level { get; set; } = 1;

        /// <summary>
        /// Stored streak: consecutive UTC days with a submission, as of <see cref="LastSubmissionAt"/>.
        /// It is not reset when the user stops playing; DTOs show <c>StreakHelper.Effective</c>, which is 0
        /// once the last submission is older than yesterday.
        /// </summary>
        public int Streak { get; set; } = 0;

        /// <summary>
        /// UTC timestamp of the most recent XP-earning submission. Streak logic compares only its <c>.Date</c>
        /// (rows written before this change hold UTC midnight).
        /// </summary>
        public DateTime? LastSubmissionAt { get; set; } = null;
    }
}
