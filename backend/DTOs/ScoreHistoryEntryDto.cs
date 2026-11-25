namespace DailyChallenges.DTOs;

public sealed record ScoreHistoryEntryDto
{
    public string ScoringDay { get; set; } = string.Empty;
    public double? ScoreValue { get; set; }
}
