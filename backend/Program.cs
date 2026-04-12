using Microsoft.EntityFrameworkCore;
using DailyChallenges.Data;
using DailyChallenges.Middleware;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
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

// User profile repository
builder.Services.AddScoped<DailyChallenges.Repositories.IUserProfileRepository, DailyChallenges.Repositories.EfUserProfileRepository>();

// Friends
builder.Services.AddScoped<DailyChallenges.Repositories.Contracts.IFriendRepository, DailyChallenges.Repositories.EfFriendRepository>();
builder.Services.AddScoped<DailyChallenges.Services.Contracts.IFriendService, DailyChallenges.Services.FriendService>();

// XP system
builder.Services.Configure<DailyChallenges.Services.XpConfig>(builder.Configuration.GetSection("Xp"));
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DailyChallenges.Services.XpConfig>>().Value;
    return new DailyChallenges.Services.LevelCalculator(cfg.LevelBase, cfg.LevelExponent);
});
// Memory cache for short-lived caches (user profile, etc.)
builder.Services.AddMemoryCache();
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

// Dashboard service
builder.Services.AddScoped<DailyChallenges.Services.Contracts.IDashboardService, DailyChallenges.Services.DashboardService>();

// Info service: HTTP client + singleton service to encapsulate changelog fetching and release-info reading
builder.Services.AddHttpClient();
builder.Services.AddSingleton<DailyChallenges.Services.Contracts.IInfoService, DailyChallenges.Services.InfoService>();

// Scoring day finalization
builder.Services.AddScoped<DailyChallenges.Services.IScoringDayFinalizerService, DailyChallenges.Services.ScoringDayFinalizerService>();
builder.Services.AddHostedService<DailyChallenges.Services.ScoringDayBackgroundService>();

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? string.Empty;
var issuer = builder.Configuration["Jwt:Issuer"];
var audience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.ContainsKey("access_token"))
                {
                    context.Token = context.Request.Cookies["access_token"];
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseCors("DefaultCors");
app.UseExceptionToHttpResponse();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
