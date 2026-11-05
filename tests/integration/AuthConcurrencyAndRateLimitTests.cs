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

    [Fact]
    public async Task Concurrent_registrations_make_one_admin_and_reject_case_insensitive_duplicates()
    {
        // Empty database: of several simultaneous first registrations exactly one becomes admin.
        var first = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => RegisterAsync($"first{i}")));
        Assert.All(first, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var admins = 0;
        foreach (var r in first)
        {
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            if (doc.RootElement.GetProperty("isAdmin").GetBoolean()) admins++;
        }
        Assert.Equal(1, admins);

        // The same name in different cases at once: one wins, the rest get 409.
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

    [Fact]
    public async Task Login_is_limited_per_client_ip_with_429_and_message()
    {
        for (var i = 0; i < RateLimitedApiFactory.PermitLimit; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync("203.0.113.10")).StatusCode);

        var limited = await LoginAsync("203.0.113.10");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        using var doc = JsonDocument.Parse(await limited.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrEmpty(doc.RootElement.GetProperty("message").GetString()));

        // Register shares the bucket.
        var register = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new { username = "limited-user", password = "password-123" })
        };
        register.Headers.Add("X-Forwarded-For", "203.0.113.10");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await _factory.CreateClient().SendAsync(register)).StatusCode);

        // Another client (X-Forwarded-For from the trusted proxy) has its own window.
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync("203.0.113.20")).StatusCode);

        // Other endpoints aren't limited.
        Assert.Equal(HttpStatusCode.OK, (await _factory.CreateClient().GetAsync("/api/games")).StatusCode);
    }
}
