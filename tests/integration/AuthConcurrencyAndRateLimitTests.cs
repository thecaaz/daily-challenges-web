using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DailyChallenges.Data;
using DailyChallenges.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DailyChallenges.IntegrationTests;

/// <summary>
/// An API host on a temporary SQLite file instead of the shared in-memory connection, so concurrent
/// requests each get their own connection (and real BEGIN IMMEDIATE locking), like production.
/// </summary>
public sealed class FileDbApiFactory : ApiFactory
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"dc-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureServices(IServiceCollection services)
    {
        ReplaceDbContext(services, o => o.UseSqlite($"Data Source={_path};Pooling=False"));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            try { File.Delete(_path + suffix); } catch (IOException) { }
        }
    }
}

public class ConcurrentRegistrationTests : IClassFixture<FileDbApiFactory>
{
    private readonly FileDbApiFactory _factory;

    public ConcurrentRegistrationTests(FileDbApiFactory factory) => _factory = factory;

    private Task<HttpResponseMessage> RegisterAsync(string username) =>
        _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new { username, password = "password-123" });

    /// <summary>
    /// Both races share the fixture's database and the first one needs it empty, so they run in this one test:
    /// of several simultaneous first registrations exactly one becomes admin; of the same name registered in
    /// different cases at once, one wins and the rest get 409.
    /// </summary>
    [Fact]
    public async Task Concurrent_registrations_make_one_admin_and_reject_case_insensitive_duplicates()
    {
        await AssertExactlyOneOfTheFirstRegistrationsBecomesAdminAsync();
        await AssertOnlyOneCaseVariantOfANameWinsAsync();
    }

    private async Task AssertExactlyOneOfTheFirstRegistrationsBecomesAdminAsync()
    {
        var first = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => RegisterAsync($"first{i}")));
        Assert.All(first, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var admins = 0;
        foreach (var r in first)
        {
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            if (doc.RootElement.GetProperty("isAdmin").GetBoolean()) admins++;
        }
        Assert.Equal(1, admins);
    }

    private async Task AssertOnlyOneCaseVariantOfANameWinsAsync()
    {
        var racers = await Task.WhenAll(new[] { "Racer", "racer", "RACER", " racer ", "rAcEr" }.Select(RegisterAsync));
        Assert.Single(racers, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(4, racers.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        await _factory.WithDbAsync(async db =>
            Assert.Equal(1, await db.Users.CountAsync(u => u.Username.ToLower() == "racer")));
    }
}

/// <summary>Rate limiting is off in "Testing" by default; this host turns it on with a small limit.</summary>
public sealed class RateLimitedApiFactory : ApiFactory
{
    public const int PermitLimit = 3;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("RateLimiting:Auth:Enabled", "true");
        builder.UseSetting("RateLimiting:Auth:PermitLimit", PermitLimit.ToString());
        builder.UseSetting("RateLimiting:Auth:WindowSeconds", "60");
    }
}

public class AuthRateLimitTests : IClassFixture<RateLimitedApiFactory>
{
    private readonly RateLimitedApiFactory _factory;

    public AuthRateLimitTests(RateLimitedApiFactory factory) => _factory = factory;

    private Task<HttpResponseMessage> LoginAsync(string clientIp)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { username = "nobody", password = "wrong-password" })
        };
        req.Headers.Add("X-Forwarded-For", clientIp);
        return _factory.CreateClient().SendAsync(req);
    }

    /// <summary>Uses up the login window of <paramref name="clientIp"/> (each test uses its own IP, as they share the limiter).</summary>
    private async Task ExhaustLoginWindowAsync(string clientIp)
    {
        for (var i = 0; i < RateLimitedApiFactory.PermitLimit; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(clientIp)).StatusCode);
    }

    [Fact]
    public async Task Login_is_limited_per_client_ip_with_429_and_message()
    {
        await ExhaustLoginWindowAsync("203.0.113.10");

        var limited = await LoginAsync("203.0.113.10");

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        using var doc = JsonDocument.Parse(await limited.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrEmpty(doc.RootElement.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Register_shares_the_login_window()
    {
        await ExhaustLoginWindowAsync("203.0.113.11");
        var register = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new { username = "limited-user", password = "password-123" })
        };
        register.Headers.Add("X-Forwarded-For", "203.0.113.11");

        var res = await _factory.CreateClient().SendAsync(register);

        Assert.Equal(HttpStatusCode.TooManyRequests, res.StatusCode);
    }

    /// <summary>The client IP comes from X-Forwarded-For, sent by the trusted proxy.</summary>
    [Fact]
    public async Task Another_client_ip_has_its_own_window()
    {
        await ExhaustLoginWindowAsync("203.0.113.12");

        var otherClient = await LoginAsync("203.0.113.20");

        Assert.Equal(HttpStatusCode.Unauthorized, otherClient.StatusCode);
    }

    [Fact]
    public async Task Endpoints_other_than_auth_are_not_limited()
    {
        await ExhaustLoginWindowAsync("203.0.113.13");

        var res = await _factory.CreateClient().GetAsync("/api/games");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
