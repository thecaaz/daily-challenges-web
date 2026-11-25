using DailyChallenges.DTOs;

namespace DailyChallenges.Services.Contracts;

public interface IInfoService
{
    /// <summary>The running release and the changelog; never throws for a missing file or an unreachable GitHub.</summary>
    Task<InfoDto> GetInfoAsync();
}

/// <summary>Contents of release-info.json.</summary>
public sealed record ReleaseInfo
{
    public string? Version { get; init; }
    public string? RawVersion { get; init; }
    public string? Commit { get; init; }
    public string? Timestamp { get; init; }
}
