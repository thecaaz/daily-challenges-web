using System;
using System.ComponentModel.DataAnnotations;

namespace DailyChallenges.Models;

public sealed class Submission
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int GameId { get; set; }

    public Game? Game { get; set; }

    /// <summary>The registered submitter; null for anonymous submissions.</summary>
    public int? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>The score as entered, in any format (e.g. "3/6", "2:34").</summary>
    [Required]
    public string Score { get; set; } = string.Empty;

    /// <summary>The numeric value parsed from <see cref="Score"/> (decimals allowed); null when unparseable, i.e. unranked.</summary>
    public double? ScoreValue { get; set; }

    public string? Username { get; set; }

    /// <summary>The screenshot, stored as a blob.</summary>
    public byte[] ScreenshotData { get; set; } = Array.Empty<byte>();
    public string ScreenshotContentType { get; set; } = string.Empty;

    /// <summary>The date-only scoring day, fixed at write time from the game's reset time.</summary>
    public DateTime ScoringDay { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>XP granted to the submitting user for this submission (null for anonymous or before XP system).</summary>
    public int? XpAwarded { get; set; }
}
