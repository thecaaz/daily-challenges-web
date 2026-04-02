using System.Threading.Tasks;

namespace DailyChallenges.Services.Contracts
{
    public interface IInfoService
    {
        Task<ReleaseInfo?> ReadReleaseInfoAsync();
        Task<string> FetchChangelogAsync();
    }

    public class ReleaseInfo
    {
        public string? Version { get; set; }
        public string? RawVersion { get; set; }
        public string? Commit { get; set; }
        public string? Timestamp { get; set; }
    }
}
