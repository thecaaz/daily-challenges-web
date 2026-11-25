using DailyChallenges.Data;
using DailyChallenges.Middleware;
using DailyChallenges.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DailyChallenges.Startup;

/// <summary>Startup steps that run on the built application: option checks, the middleware pipeline and the database migration.</summary>
public static class WebApplicationExtensions
{
    private const string MigrationLoggerCategory = "DatabaseMigration";

    /// <summary>
    /// Resolves the Jwt and Xp options now (ValidateOnStart only fires in <c>app.Run</c>), so bad settings,
    /// such as a missing JWT key in Production, fail before migrations run.
    /// </summary>
    public static WebApplication ValidateCriticalOptions(this WebApplication app)
    {
        _ = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        _ = app.Services.GetRequiredService<IOptions<XpConfig>>().Value;
        return app;
    }

    /// <summary>
    /// Forwarded headers come first, so everything after them (rate limiter, logs) sees the real client IP.
    /// Swagger is only served in Development.
    /// </summary>
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();

        app.UseCors(HostingServiceCollectionExtensions.CorsPolicyName);
        app.UseExceptionToHttpResponse();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseStaticFiles();

        app.UseRateLimiter();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        return app;
    }

    /// <summary>
    /// Runs the pre-migration data fixups, applies pending migrations and re-syncs stored user levels.
    /// A database that is not writable is logged with the fix before the startup fails.
    /// </summary>
    public static WebApplication MigrateDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(MigrationLoggerCategory);
        try
        {
            PreMigrationFixups.Run(db, logger);
            ApplyPendingMigrations(db, logger);
            UserLevelSync.Run(db, scope.ServiceProvider.GetRequiredService<LevelCalculator>(), logger);
        }
        catch (Exception ex) when (DatabaseStartupErrors.IsNotWritable(ex))
        {
            LogDatabaseNotWritable(db, logger);
            throw;
        }
        return app;
    }

    /// <summary>A failed migration is logged with the migrations that were pending, then fails the startup.</summary>
    private static void ApplyPendingMigrations(AppDbContext db, ILogger logger)
    {
        var pendingMigrations = db.Database.GetPendingMigrations().ToList();
        if (pendingMigrations.Count > 0)
            logger.LogInformation("Applying {Count} pending migration(s): {Migrations}", pendingMigrations.Count, string.Join(", ", pendingMigrations));
        try
        {
            db.Database.Migrate();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Database migration failed; pending migrations were: {Migrations}", string.Join(", ", pendingMigrations));
            throw;
        }
        if (pendingMigrations.Count > 0)
            logger.LogInformation("Database migrations applied");
    }

    /// <summary>Typically a volume created by an older image (root-owned) while the container now runs as uid 1654.</summary>
    private static void LogDatabaseNotWritable(AppDbContext db, ILogger logger) =>
        logger.LogCritical(
            "The SQLite database ({DataSource}) or its directory is not writable by the backend process (user {User}). " +
            "If the data volume was created by an older image that ran as root, hand it over once, e.g. " +
            "`docker run --rm -v daily-challenges-web_sqlite_data:/data alpine chown -R 1654:1654 /data` (see README, 'Upgrading an existing deployment').",
            db.Database.GetDbConnection().DataSource, Environment.UserName);
}
