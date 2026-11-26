using System.ComponentModel.DataAnnotations;

namespace DailyChallenges.Models;

public sealed class Game
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>The game image, stored as a blob.</summary>
    public byte[]? ScreenshotData { get; set; }
    public string? ScreenshotContentType { get; set; }

    /// <summary>The UTC time of day at which a scoring day rolls over. No time zone is stored.</summary>
    public TimeSpan ResetTime { get; set; } = TimeSpan.Zero;

    public List<Submission> Submissions { get; set; } = new();

    /// <summary>Where the game can be played or viewed.</summary>
    public string? Url { get; set; }

    /// <summary>Shown on the game detail page.</summary>
    public string? Description { get; set; }

    /// <summary>How ranks are calculated.</summary>
    public RankingMode RankingMode { get; set; } = RankingMode.Highest;
}
