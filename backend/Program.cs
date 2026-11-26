using DailyChallenges.Startup;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApiControllers()
    .AddPersistence(builder.Configuration)
    .AddFrontendCors(builder.Configuration)
    .AddSharedInfrastructure()
    .AddGameServices()
    .AddSubmissionServices()
    .AddScoringDayServices()
    .AddXpServices(builder.Configuration)
    .AddUserServices()
    .AddAchievementServices()
    .AddFriendServices()
    .AddLeagueServices()
    .AddAdminServices()
    .AddDashboardServices()
    .AddInfoServices()
    .AddSessionServices()
    .AddJwtAuthentication(builder.Configuration)
    .AddForwardedHeadersFromConfiguration()
    .AddAuthRateLimiting(builder.Configuration);

var app = builder.Build();

app.ValidateCriticalOptions()
    .UseApiPipeline()
    .MigrateDatabase()
    .Run();
