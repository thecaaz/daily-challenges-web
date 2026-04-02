using System.Text.Json;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Services
{
    public class InfoService : IInfoService
    {
        private readonly IWebHostEnvironment _env;
        private readonly IHttpClientFactory _httpFactory;
        private const string ChangelogUrl = "https://raw.githubusercontent.com/thecaaz/daily-challenges-web/refs/heads/main/CHANGELOG.md";

        private string? _cachedChangelog;
        private DateTimeOffset _cachedChangelogFetchedAt = DateTimeOffset.MinValue;
        private static readonly TimeSpan _changelogCacheDuration = TimeSpan.FromHours(2);
        private readonly SemaphoreSlim _changelogLock = new SemaphoreSlim(1, 1);

        public InfoService(IWebHostEnvironment env, IHttpClientFactory httpFactory)
        {
            _env = env;
            _httpFactory = httpFactory;
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
            catch
            {
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
                }
                catch
                {
                    // swallow network errors and return empty string
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
