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

        // ── XP / Level ──────────────────────────────────────────────────────────
        /// <summary>Lifetime XP earned across all submissions.</summary>
        public long TotalXp { get; set; } = 0;

        /// <summary>Current level, derived from TotalXp and kept in sync on every award.</summary>
        public int Level { get; set; } = 1;

        /// <summary>Number of consecutive scoring days the user has submitted on.</summary>
        public int Streak { get; set; } = 0;

        /// <summary>The scoring day (date-only, stored as UTC midnight) of the most recent XP-earning submission.</summary>
        public DateTime? LastSubmissionAt { get; set; } = null;
    }
}
