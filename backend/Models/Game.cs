using System.ComponentModel.DataAnnotations;

namespace DailyChallenges.Models
{
    public class Game
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        // Stored image as blob
        public byte[]? ScreenshotData { get; set; }
        public string? ScreenshotContentType { get; set; }

        // Daily reset time and timezone for scoring day boundaries.
        // ResetTime is a wall-clock time (HH:mm) in the specified ResetTimezoneId.
        public TimeSpan ResetTime { get; set; } = TimeSpan.Zero;
        public string ResetTimezoneId { get; set; } = "UTC";

        // Navigation property
        public List<Submission> Submissions { get; set; } = new();

        // Optional URL where the game can be played or viewed
        public string? Url { get; set; }
        // Optional description for game detail page
        public string? Description { get; set; }
        // Determines how ranks are calculated
        public RankingMode RankingMode { get; set; } = RankingMode.Highest;
    }
}
