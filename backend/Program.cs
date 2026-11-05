using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Data.Common;
using DailyChallenges.Data;
using DailyChallenges.Middleware;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using System.Text;
using DailyChallenges.Json;
using DailyChallenges.Services;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    // Every DateTime goes out as UTC with a trailing "Z" (also covers DateTime?).
    o.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var envSqlitePath = Environment.GetEnvironmentVariable("SQLITE_PATH");
    var configConn = builder.Configuration.GetConnectionString("Default");

    if (!string.IsNullOrEmpty(envSqlitePath))
    {
        options.UseSqlite($"Data Source={envSqlitePath}");
    }
    else if (!string.IsNullOrEmpty(configConn))
    {
        options.UseSqlite(configConn);
    }
    else
    {
        options.UseSqlite("Data Source=app.db");
    }
});

// CORS: allow frontend origin and credentials (adjust origin for your dev host)
var frontendOrigin = builder.Configuration["Frontend:DevOrigin"] ?? "http://localhost:5173";
builder.Services.AddCors(options => options.AddPolicy("DefaultCors", b =>
    b.WithOrigins(frontendOrigin)
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));

// Register repositories
builder.Services.AddScoped<DailyChallenges.Repositories.IGameRepository, DailyChallenges.Repositories.EfGameRepository>();
builder.Services.AddScoped<DailyChallenges.Repositories.ISubmissionRepository, DailyChallenges.Repositories.EfSubmissionRepository>();
builder.Services.AddScoped<DailyChallenges.Repositories.IXpEventRepository, DailyChallenges.Repositories.EfXpEventRepository>();
builder.Services.AddScoped<DailyChallenges.Repositories.INotificationRepository, DailyChallenges.Repositories.EfNotificationRepository>();
builder.Services.AddScoped<DailyChallenges.Repositories.IScoringDayResultRepository, DailyChallenges.Repositories.EfScoringDayResultRepository>();
builder.Services.AddScoped<DailyChallenges.Repositories.IFavoriteRepository, DailyChallenges.Repositories.EfFavoriteRepository>();
builder.Services.AddScoped<DailyChallenges.Services.IFavoriteService, DailyChallenges.Services.FavoriteService>();
// Helper services
builder.Services.AddScoped<DailyChallenges.Services.IUserSubmissionChecker, DailyChallenges.Services.UserSubmissionChecker>();
// Hidden-scores rule + signed screenshot URLs
builder.Services.AddScoped<DailyChallenges.Services.IScoreVisibilityService, DailyChallenges.Services.ScoreVisibilityService>();
builder.Services.AddSingleton<DailyChallenges.Services.IScreenshotUrlSigner, DailyChallenges.Services.ScreenshotUrlSigner>();

// User profile repository
builder.Services.AddScoped<DailyChallenges.Repositories.IUserProfileRepository, DailyChallenges.Repositories.EfUserProfileRepository>();
builder.Services.AddScoped<DailyChallenges.Repositories.Contracts.IUserRepository, DailyChallenges.Repositories.EfUserRepository>();
builder.Services.AddScoped<DailyChallenges.Repositories.Contracts.IUserAchievementRepository, DailyChallenges.Repositories.EfUserAchievementRepository>();

// Friends
builder.Services.AddScoped<DailyChallenges.Repositories.Contracts.IFriendRepository, DailyChallenges.Repositories.EfFriendRepository>();
builder.Services.AddScoped<DailyChallenges.Services.Contracts.IFriendService, DailyChallenges.Services.FriendService>();

// Leagues
builder.Services.AddScoped<DailyChallenges.Repositories.Contracts.ILeagueRepository, DailyChallenges.Repositories.EfLeagueRepository>();
builder.Services.AddScoped<DailyChallenges.Services.Contracts.ILeagueService, DailyChallenges.Services.LeagueService>();

// XP system
builder.Services.AddOptions<DailyChallenges.Services.XpConfig>()
    .Bind(builder.Configuration.GetSection("Xp"))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<DailyChallenges.Services.XpConfig>, DailyChallenges.Services.XpConfigValidator>();
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DailyChallenges.Services.XpConfig>>().Value;
    return new DailyChallenges.Services.LevelCalculator(cfg.LevelBase, cfg.LevelExponent);
});
// Memory cache for short-lived caches (user profile, etc.)
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<DailyChallenges.Services.IXpService, DailyChallenges.Services.XpService>();

// Admin users service
builder.Services.AddScoped<DailyChallenges.Services.IAdminUserService, DailyChallenges.Services.AdminUserService>();

// Register services
builder.Services.AddScoped<DailyChallenges.Services.IAuthService, DailyChallenges.Services.AuthService>();
builder.Services.AddScoped<DailyChallenges.Services.IFileStorage, DailyChallenges.Services.LocalFileStorage>();
builder.Services.AddSingleton<DailyChallenges.Services.IFileValidator, DailyChallenges.Services.FileValidator>();
builder.Services.AddScoped<DailyChallenges.Services.IGameService, DailyChallenges.Services.GameService>();
builder.Services.AddScoped<DailyChallenges.Services.ISubmissionService, DailyChallenges.Services.SubmissionService>();
builder.Services.AddScoped<DailyChallenges.Services.INotificationService, DailyChallenges.Services.NotificationService>();

// User profile service
builder.Services.AddScoped<DailyChallenges.Services.Contracts.IUserProfileService, DailyChallenges.Services.UserProfileService>();

// Admin maintenance (score re-parse)
builder.Services.AddScoped<DailyChallenges.Services.Contracts.IScoreMaintenanceService, DailyChallenges.Services.ScoreMaintenanceService>();

// Dashboard service
builder.Services.AddScoped<DailyChallenges.Services.Contracts.IDashboardService, DailyChallenges.Services.DashboardService>();

// Info service: HTTP client + singleton service to encapsulate changelog fetching and release-info reading
builder.Services.AddHttpClient(DailyChallenges.Services.InfoService.ChangelogClientName,
    c => c.Timeout = DailyChallenges.Services.InfoService.ChangelogTimeout);
builder.Services.AddSingleton<DailyChallenges.Services.Contracts.IInfoService, DailyChallenges.Services.InfoService>();

// Achievement system
builder.Services.AddScoped<DailyChallenges.Services.Contracts.IAchievementService, DailyChallenges.Services.AchievementService>();

// Scoring day finalization
builder.Services.AddScoped<DailyChallenges.Services.IScoringDayFinalizerService, DailyChallenges.Services.ScoringDayFinalizerService>();
builder.Services.AddHostedService<DailyChallenges.Services.ScoringDayBackgroundService>();

// JWT Authentication: bind typed options. The key guard lives in JwtOptionsValidator.
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer();

// Configured from IOptions<JwtOptions> (not read eagerly) so test hosts can override Jwt settings.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwt) =>
    {
        var cfg = jwt.Value;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = cfg.Issuer,
            ValidAudience = cfg.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg.Key ?? string.Empty)),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
        };
        options.Events = new JwtBearerEvents
        {
            // Only access tokens authenticate API calls. Refresh tokens (and tokens issued before
            // token_use existed) are rejected; the client then refreshes or logs in again.
            OnTokenValidated = context =>
            {
                if (context.Principal?.FindFirst(TokenClaims.TokenUse)?.Value != TokenClaims.Access)
                    context.Fail("Not an access token");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Client IPs behind nginx (the frontend container proxies /api): trust X-Forwarded-For only from one hop
// inside the configured networks (default: private ranges and loopback, which covers the Docker network).
// Expected chain: client -> [optional TLS proxy] -> frontend nginx -> backend. The frontend nginx resolves the
// real client IP itself (realip module, trusting the TLS proxy) and sends a single-value
// "X-Forwarded-For: <client ip>", so one hop (ForwardedHeaders:ForwardLimit, default 1) is all the backend
// needs to read. Raise it only if more trusted proxies append to the header before it reaches the backend.
builder.Services.AddOptions<ForwardedHeadersOptions>()
    .Configure<IConfiguration>((options, config) =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = Math.Max(1, config.GetValue("ForwardedHeaders:ForwardLimit", 1));
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        var networks = config.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>();
        if (networks == null || networks.Length == 0)
            networks = new[] { "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "127.0.0.0/8", "::1/128" };
        foreach (var cidr in networks)
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(cidr.Trim()));
    });

// Login/register brute-force protection: fixed window per client IP, 429 with a JSON message.
builder.Services.AddOptions<AuthRateLimitOptions>().Bind(builder.Configuration.GetSection(AuthRateLimitOptions.SectionName));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Too many attempts. Please wait a minute and try again." }, ct);
    };
    options.AddPolicy(AuthRateLimitOptions.PolicyName, httpContext =>
    {
        var cfg = httpContext.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;
        var enabled = cfg.Enabled ?? !httpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsEnvironment("Testing");
        if (!enabled) return RateLimitPartition.GetNoLimiter("disabled");

        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, cfg.PermitLimit),
            Window = TimeSpan.FromSeconds(Math.Max(1, cfg.WindowSeconds)),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

var app = builder.Build();

// Validate Jwt and Xp options now (ValidateOnStart only fires in app.Run) so bad settings fail before migrations run.
_ = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
_ = app.Services.GetRequiredService<IOptions<DailyChallenges.Services.XpConfig>>().Value;

// First, so everything below (rate limiter, logs) sees the real client IP.
app.UseForwardedHeaders();

app.UseCors("DefaultCors");
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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var migrationLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigration");
    try
    {
        PreMigrationFixups.Run(db, migrationLogger);

        var pendingMigrations = db.Database.GetPendingMigrations().ToList();
        if (pendingMigrations.Count > 0)
            migrationLogger.LogInformation("Applying {Count} pending migration(s): {Migrations}", pendingMigrations.Count, string.Join(", ", pendingMigrations));
        try
        {
            db.Database.Migrate();
        }
        catch (Exception ex)
        {
            migrationLogger.LogCritical(ex, "Database migration failed; pending migrations were: {Migrations}", string.Join(", ", pendingMigrations));
            throw;
        }
        if (pendingMigrations.Count > 0)
            migrationLogger.LogInformation("Database migrations applied");
        UserLevelSync.Run(db, scope.ServiceProvider.GetRequiredService<LevelCalculator>(), migrationLogger);
    }
    catch (Exception ex) when (DatabaseStartupErrors.IsNotWritable(ex))
    {
        // Typically a volume created by an older image (root-owned) while the container now runs as uid 1654.
        migrationLogger.LogCritical(
            "The SQLite database ({DataSource}) or its directory is not writable by the backend process (user {User}). " +
            "If the data volume was created by an older image that ran as root, hand it over once, e.g. " +
            "`docker run --rm -v daily-challenges-web_sqlite_data:/data alpine chown -R 1654:1654 /data` (see README, 'Upgrading an existing deployment').",
            db.Database.GetDbConnection().DataSource, Environment.UserName);
        throw;
    }
}

app.Run();
