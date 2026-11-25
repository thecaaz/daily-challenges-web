using System.Text.Json.Serialization;
using DailyChallenges.Controllers;
using DailyChallenges.Data;
using DailyChallenges.Json;
using DailyChallenges.Repositories;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Startup;

/// <summary>Registrations for the HTTP host itself: MVC, Swagger, CORS, forwarded headers and the database.</summary>
public static class HostingServiceCollectionExtensions
{
    public const string CorsPolicyName = "DefaultCors";

    private const string FrontendOriginKey = "Frontend:DevOrigin";
    private const string DefaultFrontendOrigin = "http://localhost:5173";

    private const string SqlitePathVariable = "SQLITE_PATH";
    private const string ConnectionStringName = "Default";
    private const string DefaultConnectionString = "Data Source=app.db";

    private const string ForwardLimitKey = "ForwardedHeaders:ForwardLimit";
    private const string KnownNetworksKey = "ForwardedHeaders:KnownNetworks";
    private const int DefaultForwardLimit = 1;
    private const int MinForwardLimit = 1;
    private static readonly string[] DefaultKnownNetworks =
        ["10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "127.0.0.0/8", "::1/128"];

    /// <summary>
    /// Controllers with the current-user filter. Every DateTime (also DateTime?) goes out as UTC with a
    /// trailing "Z"; nulls are omitted and reference cycles ignored.
    /// </summary>
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services.AddControllers(o => o.Filters.Add<RequireCurrentUserIdFilter>()).AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            o.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
        });
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        return services;
    }

    /// <summary>
    /// SQLite at <c>SQLITE_PATH</c> when set, else the "Default" connection string, else <c>app.db</c>.
    /// The configuration is read when the context is built, so test hosts can still override it.
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(GetConnectionString(configuration)));
        services.AddScoped<ITransactionRunner, EfTransactionRunner>();
        return services;
    }

    /// <summary>Allows the frontend origin (Frontend:DevOrigin) with credentials.</summary>
    public static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration configuration)
    {
        var frontendOrigin = configuration[FrontendOriginKey] ?? DefaultFrontendOrigin;
        return services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
            policy.WithOrigins(frontendOrigin)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));
    }

    /// <summary>
    /// Client IPs behind nginx (the frontend container proxies /api): trust X-Forwarded-For only from one hop
    /// inside the configured networks (default: private ranges and loopback, which covers the Docker network).
    /// Expected chain: client -> [optional TLS proxy] -> frontend nginx -> backend. The frontend nginx resolves the
    /// real client IP itself (realip module, trusting the TLS proxy) and sends a single-value
    /// "X-Forwarded-For: &lt;client ip&gt;", so one hop (ForwardedHeaders:ForwardLimit, default 1) is all the backend
    /// needs to read. Raise it only if more trusted proxies append to the header before it reaches the backend.
    /// </summary>
    public static IServiceCollection AddForwardedHeadersFromConfiguration(this IServiceCollection services)
    {
        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = Math.Max(MinForwardLimit, config.GetValue(ForwardLimitKey, DefaultForwardLimit));
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
                foreach (var cidr in GetKnownNetworks(config))
                    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(cidr.Trim()));
            });
        return services;
    }

    private static string GetConnectionString(IConfiguration configuration)
    {
        var sqlitePath = Environment.GetEnvironmentVariable(SqlitePathVariable);
        if (!string.IsNullOrEmpty(sqlitePath)) return $"Data Source={sqlitePath}";

        var configured = configuration.GetConnectionString(ConnectionStringName);
        return !string.IsNullOrEmpty(configured) ? configured : DefaultConnectionString;
    }

    private static string[] GetKnownNetworks(IConfiguration config)
    {
        var networks = config.GetSection(KnownNetworksKey).Get<string[]>();
        return networks is { Length: > 0 } ? networks : DefaultKnownNetworks;
    }
}
