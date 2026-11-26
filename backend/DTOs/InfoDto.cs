namespace DailyChallenges.DTOs;

public sealed record InfoDto
{
    public string Version { get; init; } = string.Empty;
    public string? RawVersion { get; init; }
    public string? Commit { get; init; }
    public string? Timestamp { get; init; }
    public string Changelog { get; init; } = string.Empty;
}
