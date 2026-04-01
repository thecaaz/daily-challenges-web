using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DailyChallenges.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InfoController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string ChangelogUrl = "https://raw.githubusercontent.com/thecaaz/daily-challenges-web/refs/heads/main/CHANGELOG.md";

        private static string? _cachedChangelog;
        private static DateTimeOffset _cachedChangelogFetchedAt = DateTimeOffset.MinValue;
        private static readonly TimeSpan _changelogCacheDuration = TimeSpan.FromHours(2);
        private static readonly SemaphoreSlim _changelogLock = new SemaphoreSlim(1, 1);

        public InfoController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var release = await ReadReleaseInfoAsync();
            var changelog = await FetchChangelogAsync();

            return Ok(new
            {
                version = release?.Version ?? "dev",
                rawVersion = release?.RawVersion,
                commit = release?.Commit,
                timestamp = release?.Timestamp,
                changelog
            });
        }

        private async Task<ReleaseInfo?> ReadReleaseInfoAsync()
        {
            var path = Path.Combine(_env.ContentRootPath, "release-info.json");
            if (!System.IO.File.Exists(path)) return null;
            try
            {
                var txt = await System.IO.File.ReadAllTextAsync(path);
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<ReleaseInfo>(txt, opts);
            }
            catch
            {
                return null;
            }
        }

        private async Task<string> FetchChangelogAsync()
        {
            var now = DateTimeOffset.UtcNow;
            if (_cachedChangelog != null && (now - _cachedChangelogFetchedAt) < _changelogCacheDuration)
            {
                return _cachedChangelog;
            }

            await _changelogLock.WaitAsync();
            try
            {
                // Re-check after acquiring lock
                now = DateTimeOffset.UtcNow;
                if (_cachedChangelog != null && (now - _cachedChangelogFetchedAt) < _changelogCacheDuration)
                {
                    return _cachedChangelog;
                }

                try
                {
                    var resp = await _httpClient.GetAsync(ChangelogUrl);
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
                    // ignore remote fetch errors and return empty string
                }

                return string.Empty;
            }
            finally
            {
                _changelogLock.Release();
            }
        }

        private class ReleaseInfo
        {
            public string Version { get; set; }
            public string RawVersion { get; set; }
            public string Commit { get; set; }
            public string Timestamp { get; set; }
        }
    }
}
