using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DailyChallenges.Models
{
    public class Submission
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int GameId { get; set; }

        public Game? Game { get; set; }

        // Allow flexible score formats (e.g., "3/6", "2:34")
        [Required]
        public string Score { get; set; } = string.Empty;

        public string? Username { get; set; }

        // Relative URL to screenshot (wwwroot)
        public string? ScreenshotUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
