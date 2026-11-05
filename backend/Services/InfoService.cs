using System.Text.Json;
using DailyChallenges.Services.Contracts;
using Microsoft.Extensions.Logging;

namespace DailyChallenges.Services
{
    public class InfoService : IInfoService
    {
        /// <summary>Name of the HttpClient used for the changelog (registered with a short timeout in Program.cs).</summary>
        public const string ChangelogClientName = "changelog";
        /// <summary>Timeout of the changelog HttpClient.</summary>
        public static readonly TimeSpan ChangelogTimeout = TimeSpan.FromSeconds(5);
        /// <summary>A successful fetch is reused this long.</summary>
        public static readonly TimeSpan ChangelogCacheDuration = TimeSpan.FromHours(2);
        /// <summary>After a failed fetch, no new fetch is attempted for this long (the last good copy is served).</summary>
        public static readonly TimeSpan ChangelogFailureBackoff = TimeSpan.FromMinutes(5);

        private readonly IWebHostEnvironment _env;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<InfoService> _logger;
        private readonly TimeProvider _time;
        private const string ChangelogUrl = "https://raw.githubusercontent.com/thecaaz/daily-challenges-web/refs/heads/main/CHANGELOG.md";

        // All fields below are guarded by _gate. The lock is only held for bookkeeping, never across the HTTP call.
        private readonly object _gate = new();
        private string? _cachedChangelog;
        private DateTimeOffset _cachedChangelogFetchedAt = DateTimeOffset.MinValue;
        private DateTimeOffset _lastFailureAt = DateTimeOffset.MinValue;
        private Task<string>? _refreshTask;

        public InfoService(IWebHostEnvironment env, IHttpClientFactory httpFactory, ILogger<InfoService> logger, TimeProvider time)
        {
            _env = env;
            _httpFactory = httpFactory;
            _logger = logger;
            _time = time;
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

        /// <summary>
        /// The changelog markdown. Served from cache while it is fresh; otherwise one shared refresh runs
        /// (concurrent callers await the same fetch). A failed refresh returns the last good copy (empty
        /// when there never was one) and no new fetch is tried for <see cref="ChangelogFailureBackoff"/>.
        /// </summary>
        public Task<string> FetchChangelogAsync()
        {
            lock (_gate)
            {
                var now = _time.GetUtcNow();
                if (_cachedChangelog != null && now - _cachedChangelogFetchedAt < ChangelogCacheDuration)
                    return Task.FromResult(_cachedChangelog);

                if (now - _lastFailureAt < ChangelogFailureBackoff)
                    return Task.FromResult(_cachedChangelog ?? string.Empty);

                // A finished task is never reused: it belongs to an earlier refresh.
                if (_refreshTask == null || _refreshTask.IsCompleted)
                    _refreshTask = RefreshChangelogAsync();
                return _refreshTask;
            }
        }

        private async Task<string> RefreshChangelogAsync()
        {
            try
            {
                var client = _httpFactory.CreateClient(ChangelogClientName);
                using var resp = await client.GetAsync(ChangelogUrl).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    var content = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    lock (_gate)
                    {
                        _cachedChangelog = content;
                        _cachedChangelogFetchedAt = _time.GetUtcNow();
                        _lastFailureAt = DateTimeOffset.MinValue;
                    }
                    return content;
                }

                _logger.LogWarning("Fetching changelog returned non-success status {StatusCode} from {Url}", resp.StatusCode, ChangelogUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch changelog from {Url}", ChangelogUrl);
            }

            lock (_gate)
            {
                _lastFailureAt = _time.GetUtcNow();
                return _cachedChangelog ?? string.Empty;
            }
        }
    }
}
