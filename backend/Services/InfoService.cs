using System.Text.Json;
using DailyChallenges.Services.Contracts;
using Microsoft.Extensions.Logging;

namespace DailyChallenges.Services
{
    public class InfoService : IInfoService
    {
        private readonly IWebHostEnvironment _env;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<InfoService> _logger;
        private const string ChangelogUrl = "https://raw.githubusercontent.com/thecaaz/daily-challenges-web/refs/heads/main/CHANGELOG.md";

        private string? _cachedChangelog;
        private DateTimeOffset _cachedChangelogFetchedAt = DateTimeOffset.MinValue;
        private static readonly TimeSpan _changelogCacheDuration = TimeSpan.FromHours(2);
        private readonly SemaphoreSlim _changelogLock = new SemaphoreSlim(1, 1);

        public InfoService(IWebHostEnvironment env, IHttpClientFactory httpFactory, ILogger<InfoService> logger)
        {
            _env = env;
            _httpFactory = httpFactory;
            _logger = logger;
        }

        public async Task<ReleaseInfo?> ReadReleaseInfoAsync()
        {
            var path = Path.Combine(_env.ContentRootPath, "release-info.json");
            if (!File.Exists(path)) return null;
            try
            {
                var txt = await File.ReadAllTextAsync(path);
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<ReleaseInfo>(txt, opts);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read release-info.json at {Path}", path);
                return null;
            }
        }

        public async Task<string> FetchChangelogAsync()
        {
            var now = DateTimeOffset.UtcNow;
            if (_cachedChangelog != null && (now - _cachedChangelogFetchedAt) < _changelogCacheDuration)
            {
                return _cachedChangelog;
            }

            await _changelogLock.WaitAsync();
            try
            {
                now = DateTimeOffset.UtcNow;
                if (_cachedChangelog != null && (now - _cachedChangelogFetchedAt) < _changelogCacheDuration)
                {
                    return _cachedChangelog;
                }

                try
                {
                    var client = _httpFactory.CreateClient();
                    var resp = await client.GetAsync(ChangelogUrl);
                    if (resp.IsSuccessStatusCode)
                    {
                        var content = await resp.Content.ReadAsStringAsync();
                        _cachedChangelog = content;
                        _cachedChangelogFetchedAt = DateTimeOffset.UtcNow;
                        return content;
                    }
                    else
                    {
                        _logger.LogWarning("Fetching changelog returned non-success status {StatusCode} from {Url}", resp.StatusCode, ChangelogUrl);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch changelog from {Url}", ChangelogUrl);
                }

                return string.Empty;
            }
            finally
            {
                _changelogLock.Release();
            }
        }
    }
}
