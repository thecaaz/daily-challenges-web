using System.ComponentModel.DataAnnotations;

namespace DailyChallenges.Models
{
    /// <summary>
    /// Idempotency guard: tracks which game+scoring-day combos have been finalized
    /// so the background job never double-processes a day.
    /// </summary>
    public class ScoringDayResult
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int GameId { get; set; }
        public Game? Game { get; set; }

        public DateTime ScoringDay { get; set; }

        public int? WinnerUserId { get; set; }
        public User? WinnerUser { get; set; }

        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    }
}
