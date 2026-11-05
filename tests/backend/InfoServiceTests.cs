using System.Net;
using DailyChallenges.Services;
using DailyChallenges.TestUtilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DailyChallenges.Tests;

/// <summary>Changelog caching: a failed refresh serves the last good copy and backs off (B9).</summary>
public class InfoServiceTests
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

    private static (InfoService Service, FakeHandler Handler, FakeFactory Factory, FixedTimeProvider Clock) Create()
    {
        var handler = new FakeHandler();
        var factory = new FakeFactory(handler);
        var clock = new FixedTimeProvider(Start);
        var service = new InfoService(Mock.Of<IWebHostEnvironment>(), factory, NullLogger<InfoService>.Instance, clock);
        return (service, handler, factory, clock);
    }

    [Fact]
    public async Task Fresh_cache_is_served_without_a_new_fetch_and_uses_the_named_client()
    {
        var (svc, handler, factory, clock) = Create();
        handler.Ok("v1");

        Assert.Equal("v1", await svc.FetchChangelogAsync());
        clock.Advance(InfoService.ChangelogCacheDuration - TimeSpan.FromSeconds(1));
        Assert.Equal("v1", await svc.FetchChangelogAsync());

        Assert.Equal(1, handler.Calls);
        Assert.All(factory.Names, n => Assert.Equal(InfoService.ChangelogClientName, n));
    }

    [Fact]
    public async Task Failed_refresh_returns_the_cached_changelog_and_backs_off_for_five_minutes()
    {
        var (svc, handler, _, clock) = Create();
        handler.Ok("v1");
        Assert.Equal("v1", await svc.FetchChangelogAsync());

        // Cache expired, the refresh fails: the last good copy is served, not an empty string.
        clock.Advance(InfoService.ChangelogCacheDuration);
        handler.Status(HttpStatusCode.BadGateway);
        Assert.Equal("v1", await svc.FetchChangelogAsync());
        Assert.Equal(2, handler.Calls);

        // Within the back-off: no new fetch.
        clock.Advance(TimeSpan.FromMinutes(4));
        Assert.Equal("v1", await svc.FetchChangelogAsync());
        Assert.Equal(2, handler.Calls);

        // After the back-off: retried; an exception also falls back to the cache.
        clock.Advance(TimeSpan.FromMinutes(1));
        handler.Throw();
        Assert.Equal("v1", await svc.FetchChangelogAsync());
        Assert.Equal(3, handler.Calls);

        // Next retry succeeds and replaces the cache.
        clock.Advance(InfoService.ChangelogFailureBackoff);
        handler.Ok("v2");
        Assert.Equal("v2", await svc.FetchChangelogAsync());
        Assert.Equal(4, handler.Calls);
    }

    [Fact]
    public async Task Failure_without_any_cached_copy_returns_empty_and_backs_off()
    {
        var (svc, handler, _, clock) = Create();
        handler.Throw();

        Assert.Equal(string.Empty, await svc.FetchChangelogAsync());
        Assert.Equal(string.Empty, await svc.FetchChangelogAsync());
        Assert.Equal(1, handler.Calls);

        clock.Advance(InfoService.ChangelogFailureBackoff);
        handler.Ok("v1");
        Assert.Equal("v1", await svc.FetchChangelogAsync());
        Assert.Equal(2, handler.Calls);
    }
}
