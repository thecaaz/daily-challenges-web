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

        // Daily reset time in UTC (time-of-day). Timezone is no longer stored.
        // ResetTime represents the UTC time-of-day at which a scoring day rolls over.
        public TimeSpan ResetTime { get; set; } = TimeSpan.Zero;

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
