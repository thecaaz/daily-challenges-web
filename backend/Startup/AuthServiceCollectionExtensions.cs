using System.Text;
using System.Threading.RateLimiting;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services;
using DailyChallenges.Services.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DailyChallenges.Startup;

/// <summary>Registration, login and refresh sessions, JWT bearer authentication and the login rate limit.</summary>
public static class AuthServiceCollectionExtensions
{
    private const string RateLimitedMessage = "Too many attempts. Please wait a minute and try again.";
    private const string RateLimitDisabledPartition = "disabled";
    private const string UnknownClientIp = "unknown";
    private const string TestingEnvironment = "Testing";
    private const string NotAnAccessTokenMessage = "Not an access token";
    private const int MinPermitLimit = 1;
    private const int MinWindowSeconds = 1;

    public static IServiceCollection AddSessionServices(this IServiceCollection services)
    {
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<ILoginService, LoginService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<SessionIssuer>();
        services.AddScoped<RefreshTokenRevocations>();
        services.AddScoped<ISessionRepository, EfSessionRepository>();
        services.AddSingleton<JwtTokenFactory>();
        services.AddSingleton<RefreshTokenReader>();
        services.AddSingleton<RefreshTokenCookie>();
        return services;
    }

    /// <summary>
    /// Binds <see cref="JwtOptions"/> (the key guard, including the Production fail-fast, lives in
    /// <see cref="JwtOptionsValidator"/>) and configures the bearer handler from <c>IOptions&lt;JwtOptions&gt;</c>
    /// rather than reading it eagerly, so test hosts can override the Jwt settings.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwt) =>
            {
                options.TokenValidationParameters = BuildTokenValidationParameters(jwt.Value);
                options.Events = new JwtBearerEvents { OnTokenValidated = RejectNonAccessTokens };
            });

        services.AddAuthorization();
        return services;
    }

    /// <summary>Login/register brute-force protection: a fixed window per client IP, answered with 429 and a JSON message.</summary>
    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthRateLimitOptions>().Bind(configuration.GetSection(AuthRateLimitOptions.SectionName));
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRateLimitedResponseAsync;
            options.AddPolicy(AuthRateLimitOptions.PolicyName, GetAuthPartition);
        });
        return services;
    }

    private static TokenValidationParameters BuildTokenValidationParameters(JwtOptions jwt) => new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key ?? string.Empty)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
    };

    /// <summary>
    /// Only access tokens authenticate API calls. Refresh tokens (and tokens issued before token_use existed)
    /// are rejected; the client then refreshes or logs in again.
    /// </summary>
    private static Task RejectNonAccessTokens(TokenValidatedContext context)
    {
        if (context.Principal?.FindFirst(TokenClaims.TokenUse)?.Value != TokenClaims.Access)
            context.Fail(NotAnAccessTokenMessage);
        return Task.CompletedTask;
    }

    private static async ValueTask WriteRateLimitedResponseAsync(OnRejectedContext context, CancellationToken ct)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        await context.HttpContext.Response.WriteAsJsonAsync(new { message = RateLimitedMessage }, ct);
    }

    /// <summary>Enabled unless configured otherwise, except in the Testing environment.</summary>
    private static RateLimitPartition<string> GetAuthPartition(HttpContext httpContext)
    {
        var cfg = httpContext.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;
        var isEnabled = cfg.Enabled ?? !httpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsEnvironment(TestingEnvironment);
        if (!isEnabled) return RateLimitPartition.GetNoLimiter(RateLimitDisabledPartition);

        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? UnknownClientIp;
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(MinPermitLimit, cfg.PermitLimit),
            Window = TimeSpan.FromSeconds(Math.Max(MinWindowSeconds, cfg.WindowSeconds)),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    }
}
