using System.Net;
using DailyChallenges.Services;
using DailyChallenges.Services.Contracts;
using DailyChallenges.TestUtilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DailyChallenges.Tests;

/// <summary>Changelog caching: a failed refresh serves the last good copy and backs off (B9).</summary>
public class ChangelogAndReleaseInfoTests
{
    private static readonly DateTime Start = new(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>Answers each request with the next queued result (a response or an exception) and counts calls.</summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new();
        public int Calls { get; private set; }

        public void Ok(string body) => _responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        public void Status(HttpStatusCode code) => _responses.Enqueue(() => new HttpResponseMessage(code));
        public void Throw() => _responses.Enqueue(() => throw new HttpRequestException("network down"));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_responses.Dequeue()());
        }
    }

    private sealed class FakeFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public List<string> Names { get; } = new();
        public FakeFactory(HttpMessageHandler handler) => _handler = handler;

        public HttpClient CreateClient(string name)
        {
            Names.Add(name);
            return new HttpClient(_handler, disposeHandler: false);
        }
    }

    private static (ChangelogCache Service, FakeHandler Handler, FakeFactory Factory, FixedTimeProvider Clock) Create()
    {
        var handler = new FakeHandler();
        var factory = new FakeFactory(handler);
        var clock = new FixedTimeProvider(Start);
        var service = new ChangelogCache(factory, NullLogger<ChangelogCache>.Instance, clock);
        return (service, handler, factory, clock);
    }

    [Fact]
    public async Task Fresh_cache_is_served_without_a_new_fetch_and_uses_the_named_client()
    {
        var (svc, handler, factory, clock) = Create();
        handler.Ok("v1");

        Assert.Equal("v1", await svc.GetChangelogAsync());
        clock.Advance(ChangelogCache.CacheDuration - TimeSpan.FromSeconds(1));
        Assert.Equal("v1", await svc.GetChangelogAsync());

        Assert.Equal(1, handler.Calls);
        Assert.All(factory.Names, n => Assert.Equal(ChangelogCache.ClientName, n));
    }

    /// <summary>Caches "v1", lets it expire and answers the refresh with a 502 (fetch #2).</summary>
    private static async Task<(ChangelogCache Service, FakeHandler Handler, FixedTimeProvider Clock)> CreateAfterFailedRefreshAsync()
    {
        var (svc, handler, _, clock) = Create();
        handler.Ok("v1");
        await svc.GetChangelogAsync();
        clock.Advance(ChangelogCache.CacheDuration);
        handler.Status(HttpStatusCode.BadGateway);
        return (svc, handler, clock);
    }

    [Fact]
    public async Task Failed_refresh_returns_the_last_good_changelog()
    {
        var (svc, handler, _) = await CreateAfterFailedRefreshAsync();

        var changelog = await svc.GetChangelogAsync();

        Assert.Equal("v1", changelog);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Failed_refresh_does_not_fetch_again_within_the_backoff()
    {
        var (svc, handler, clock) = await CreateAfterFailedRefreshAsync();
        await svc.GetChangelogAsync();
        clock.Advance(ChangelogCache.FailureBackoff - TimeSpan.FromMinutes(1));

        var changelog = await svc.GetChangelogAsync();

        Assert.Equal("v1", changelog);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Fetch_throwing_after_the_backoff_returns_the_last_good_changelog()
    {
        var (svc, handler, clock) = await CreateAfterFailedRefreshAsync();
        await svc.GetChangelogAsync();
        clock.Advance(ChangelogCache.FailureBackoff);
        handler.Throw();

        var changelog = await svc.GetChangelogAsync();

        Assert.Equal("v1", changelog);
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task Successful_retry_after_the_backoff_replaces_the_cached_changelog()
    {
        var (svc, handler, clock) = await CreateAfterFailedRefreshAsync();
        await svc.GetChangelogAsync();
        clock.Advance(ChangelogCache.FailureBackoff);
        handler.Ok("v2");

        var changelog = await svc.GetChangelogAsync();

        Assert.Equal("v2", changelog);
        Assert.Equal(3, handler.Calls);
    }

    private static async Task<ReleaseInfo?> ReadReleaseInfoAsync(string? fileContent)
    {
        var root = Directory.CreateTempSubdirectory("release-info-tests").FullName;
        try
        {
            if (fileContent != null) await File.WriteAllTextAsync(Path.Combine(root, "release-info.json"), fileContent);
            var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == root);
            var service = new ReleaseInfoReader(env, NullLogger<ReleaseInfoReader>.Instance);
            return await service.ReadAsync();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Release_info_is_null_without_the_file()
    {
        Assert.Null(await ReadReleaseInfoAsync(null));
    }

    [Fact]
    public async Task Release_info_is_null_when_the_file_is_not_json()
    {
        Assert.Null(await ReadReleaseInfoAsync("not json"));
    }

    [Fact]
    public async Task Release_info_is_read_case_insensitively()
    {
        var info = await ReadReleaseInfoAsync("""{"VERSION":"1.2.3","rawVersion":"v1.2.3","Commit":"abc","timestamp":"t"}""");

        Assert.NotNull(info);
        Assert.Equal("1.2.3", info.Version);
        Assert.Equal("v1.2.3", info.RawVersion);
        Assert.Equal("abc", info.Commit);
        Assert.Equal("t", info.Timestamp);
    }

    [Fact]
    public async Task Failure_without_any_cached_copy_returns_empty_and_backs_off()
    {
        var (svc, handler, _, clock) = Create();
        handler.Throw();

        Assert.Equal(string.Empty, await svc.GetChangelogAsync());
        Assert.Equal(string.Empty, await svc.GetChangelogAsync());
        Assert.Equal(1, handler.Calls);

        clock.Advance(ChangelogCache.FailureBackoff);
        handler.Ok("v1");
        Assert.Equal("v1", await svc.GetChangelogAsync());
        Assert.Equal(2, handler.Calls);
    }
}
