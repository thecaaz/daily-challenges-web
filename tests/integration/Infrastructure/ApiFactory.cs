using DailyChallenges.Data;
using DailyChallenges.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DailyChallenges.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API in memory. Each factory owns one open in-memory SQLite connection,
/// and Program.cs runs Migrate() against it, so raw-SQL migrations and filtered indexes exist.
/// Use as an IClassFixture; tests in the same class share the database.
/// Subclass and override <see cref="ConfigureServices"/> to swap more services (e.g. the clock).
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string EnvironmentName = "Testing";
    public const string JwtKey = "integration-tests-only-signing-key-0123456789";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ApiFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("Jwt:Key", JwtKey);

        builder.ConfigureTestServices(services =>
        {
            // Swap the database for the shared in-memory connection.
            ReplaceDbContext(services, o => o.UseSqlite(_connection));

            // No background finalization while tests run.
            var hosted = services
                .Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ScoringDayBackgroundService))
                .ToList();
            foreach (var d in hosted) services.Remove(d);

            ConfigureServices(services);
        });
    }

    /// <summary>
    /// Replaces the app's AppDbContext registration. Since EF Core 9, AddDbContext also registers an
    /// IDbContextOptionsConfiguration per call and applies them all, so those must go too or Program.cs's
    /// UseSqlite(...) would still be applied alongside the replacement.
    /// </summary>
    protected static void ReplaceDbContext(IServiceCollection services, Action<DbContextOptionsBuilder> configure)
    {
        services.RemoveAll<DbContextOptions<AppDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
        services.AddDbContext<AppDbContext>(configure);
    }

    /// <summary>Extra test-service overrides, applied after the defaults above.</summary>
    protected virtual void ConfigureServices(IServiceCollection services) { }

    /// <summary>Runs <paramref name="action"/> with a fresh scoped AppDbContext (for seeding and assertions).</summary>
    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(db);
    }

    /// <summary>An HttpClient that sends a Bearer token for the given user.</summary>
    public HttpClient CreateAuthenticatedClient(int userId, string username = "tester", bool isAdmin = false)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestAuth.CreateToken(Services, userId, username, isAdmin));
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
