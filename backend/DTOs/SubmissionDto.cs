namespace DailyChallenges.DTOs;

public sealed record SubmissionDto
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public int? UserId { get; set; }
    /// <summary>Null when the score is hidden (current scoring day, caller has not submitted).</summary>
    public string? Score { get; set; } = string.Empty;
    public double? ScoreValue { get; set; }
    /// <summary>1-based rank for scored submissions (computed server-side when applicable).</summary>
    public int? Rank { get; set; }
    /// <summary>True when this submission is the winning submission for its scoring day.</summary>
    public bool IsDayWinner { get; set; }
    public string? Username { get; set; }
    public string? ScreenshotUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>Server-computed scoring day (YYYY-MM-DD) according to game's reset settings.</summary>
    public string? ScoringDay { get; set; }
}
