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

        // The game's scoring day the XP was earned for ("submission" and "day_win"; null for admin adjustments)
        public DateTime? ScoringDay { get; set; }

        /// <summary>XP delta actually applied (positive = gain, negative = deduction). For admin adjustments
        /// this is the delta after clamping TotalXp at 0; the requested delta is in <see cref="Details"/>.</summary>
        public int Amount { get; set; }

        /// <summary>
        /// Discriminator. Known values: "submission", "day_win", "admin_adjustment", "submission_revoked".
        /// The streak bonus is folded into the "submission" amount; there is no separate "streak_bonus" event.
        /// At most one "day_win" per (UserId, GameId, ScoringDay) (filtered unique index).
        /// </summary>
        [Required]
        public string EventType { get; set; } = string.Empty;

        /// <summary>Freeform context (e.g. streak count, admin reason, etc.).</summary>
        public string? Details { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
