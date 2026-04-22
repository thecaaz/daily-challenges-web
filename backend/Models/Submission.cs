using System;
using System.ComponentModel.DataAnnotations;

namespace DailyChallenges.Models
{
    public class Submission
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int GameId { get; set; }

        public Game? Game { get; set; }

        // If set, this links the submission to a registered user
        public int? UserId { get; set; }
        public User? User { get; set; }

        // Allow flexible score formats (e.g., "3/6", "2:34")
        [Required]
        public string Score { get; set; } = string.Empty;

        // Numeric score value parsed when the score can be represented as an integer
        public int? ScoreValue { get; set; }

        public string? Username { get; set; }

        // Screenshot stored as blob
        public byte[] ScreenshotData { get; set; } = Array.Empty<byte>();
        public string ScreenshotContentType { get; set; } = string.Empty;

        // Date-only scoring day calculated based on the game's reset time/timezone.
        public DateTime ScoringDay { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>XP granted to the submitting user for this submission (null for anonymous or before XP system).</summary>
        public int? XpAwarded { get; set; }
    }
}
