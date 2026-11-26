using DailyChallenges.DTOs;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Services;

public sealed class InfoService(ReleaseInfoReader releaseInfo, ChangelogCache changelog) : IInfoService
{
    /// <summary>Reported when there is no release-info.json (local builds).</summary>
    public const string DevelopmentVersion = "dev";

    public async Task<InfoDto> GetInfoAsync()
    {
        var release = await releaseInfo.ReadAsync();
        return new InfoDto
        {
            Version = release?.Version ?? DevelopmentVersion,
            RawVersion = release?.RawVersion,
            Commit = release?.Commit,
            Timestamp = release?.Timestamp,
            Changelog = await changelog.GetChangelogAsync()
        };
    }
}
