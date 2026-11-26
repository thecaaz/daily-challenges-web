namespace DailyChallenges.DTOs;

public sealed record GameDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Optional external URL for the game.</summary>
    public string? Url { get; set; }
    /// <summary>Optional description shown on the game detail page.</summary>
    public string? Description { get; set; }
    /// <summary>URL to fetch the image blob if present: GET /api/games/{id}/image.</summary>
    public string? ImageUrl { get; set; }
    /// <summary>Reset time (HH:mm) in UTC for scoring day boundaries.</summary>
    public string? ResetTime { get; set; }
    /// <summary>Current scoring day computed on server (YYYY-MM-DD) based on ResetTime (UTC).</summary>
    public string? CurrentScoringDay { get; set; }
    /// <summary>UTC range for the current scoring day in ISO 8601 (start inclusive, end exclusive).</summary>
    public string? CurrentScoringDayUtcStart { get; set; }
    public string? CurrentScoringDayUtcEnd { get; set; }
    /// <summary>Whether the current authenticated user has submitted for the CurrentScoringDay.</summary>
    public bool HasSubmittedForLatest { get; set; }
    public List<SubmissionDto>? Submissions { get; set; }
    /// <summary>How the daily winner is determined: "highest" or "lowest".</summary>
    public string RankingMode { get; set; } = "highest";
    /// <summary>Whether the current authenticated user has favorited this game.</summary>
    public bool IsFavorite { get; set; } = false;
}
