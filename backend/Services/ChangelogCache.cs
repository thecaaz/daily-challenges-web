namespace DailyChallenges.Services;

/// <summary>
/// The changelog markdown, fetched from GitHub. Served from cache while it is fresh; otherwise one shared
/// refresh runs (concurrent callers await the same fetch). A failed refresh returns the last good copy
/// (empty when there never was one) and no new fetch is tried for <see cref="FailureBackoff"/>.
/// Registered as a singleton. All mutable fields are guarded by <c>_gate</c>, which is only held for
/// bookkeeping, never across the HTTP call.
/// </summary>
public sealed class ChangelogCache(IHttpClientFactory httpFactory, ILogger<ChangelogCache> logger, TimeProvider time)
{
    /// <summary>Name of the HttpClient used for the changelog (registered with <see cref="Timeout"/> in Program.cs).</summary>
    public const string ClientName = "changelog";
    /// <summary>Timeout of the changelog HttpClient.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    /// <summary>A successful fetch is reused this long.</summary>
    public static readonly TimeSpan CacheDuration = TimeSpan.FromHours(2);
    /// <summary>After a failed fetch, no new fetch is attempted for this long (the last good copy is served).</summary>
    public static readonly TimeSpan FailureBackoff = TimeSpan.FromMinutes(5);

    private const string ChangelogUrl = "https://raw.githubusercontent.com/thecaaz/daily-challenges-web/refs/heads/main/CHANGELOG.md";

    private readonly object _gate = new();
    private string? _cachedChangelog;
    private DateTimeOffset _fetchedAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastFailureAt = DateTimeOffset.MinValue;
    private Task<string>? _refreshTask;

    public Task<string> GetChangelogAsync()
    {
        lock (_gate)
        {
            var now = time.GetUtcNow();
            if (_cachedChangelog != null && now - _fetchedAt < CacheDuration)
                return Task.FromResult(_cachedChangelog);

            if (now - _lastFailureAt < FailureBackoff)
                return Task.FromResult(LastGoodCopy());

            return JoinOrStartRefresh();
        }
    }

    /// <summary>A finished task is never reused: it belongs to an earlier refresh.</summary>
    private Task<string> JoinOrStartRefresh()
    {
        if (_refreshTask is { IsCompleted: false } running)
            return running;

        _refreshTask = RefreshAsync();
        return _refreshTask;
    }

    private async Task<string> RefreshAsync()
    {
        var content = await TryFetchAsync().ConfigureAwait(false);
        lock (_gate)
        {
            if (content == null)
            {
                _lastFailureAt = time.GetUtcNow();
                return LastGoodCopy();
            }

            _cachedChangelog = content;
            _fetchedAt = time.GetUtcNow();
            _lastFailureAt = DateTimeOffset.MinValue;
            return content;
        }
    }

    /// <summary>The changelog, or null when GitHub could not be reached or answered with an error status.</summary>
    private async Task<string?> TryFetchAsync()
    {
        try
        {
            var client = httpFactory.CreateClient(ClientName);
            using var response = await client.GetAsync(ChangelogUrl).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            logger.LogWarning("Fetching changelog returned non-success status {StatusCode} from {Url}", response.StatusCode, ChangelogUrl);
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Failed to fetch changelog from {Url}", ChangelogUrl);
            return null;
        }
    }

    private string LastGoodCopy() => _cachedChangelog ?? string.Empty;
}
