using System.ComponentModel.DataAnnotations;

namespace DailyChallenges.Models
{
    /// <summary>
    /// Immutable audit record of every XP change for a user.
    /// </summary>
    public class XpEvent
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }
        public User? User { get; set; }

        // The submission that triggered this event (null for admin adjustments etc.)
        public int? SubmissionId { get; set; }
        public Submission? Submission { get; set; }

        public int? GameId { get; set; }
        public Game? Game { get; set; }

        // The scoring day the XP was earned for (null for non-submission events)
        public DateTime? ScoringDay { get; set; }

        /// <summary>XP delta (positive = gain, negative = deduction).</summary>
        public int Amount { get; set; }

        /// <summary>
        /// Discriminator. Known values: "submission", "streak_bonus", "admin_adjustment".
        /// </summary>
        [Required]
        public string EventType { get; set; } = string.Empty;

        /// <summary>Freeform context (e.g. streak count, admin reason, etc.).</summary>
        public string? Details { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
