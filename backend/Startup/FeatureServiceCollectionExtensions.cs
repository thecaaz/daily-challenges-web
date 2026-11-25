using DailyChallenges.Achievements;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services;
using DailyChallenges.Services.Contracts;
using DailyChallenges.Services.Dashboard;
using Microsoft.Extensions.Options;

namespace DailyChallenges.Startup;

/// <summary>The application's services and repositories, grouped by feature.</summary>
public static class FeatureServiceCollectionExtensions
{
    /// <summary>The clock, the short-lived in-memory caches (user profile, etc.) and uploaded-file handling.</summary>
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IFileValidator, FileValidator>();
        return services;
    }

    public static IServiceCollection AddGameServices(this IServiceCollection services)
    {
        services.AddScoped<IGameRepository, EfGameRepository>();
        services.AddScoped<IGameQueryService, GameQueryService>();
        services.AddScoped<IGameCommandService, GameCommandService>();
        services.AddScoped<IGameHighscoreService, GameHighscoreService>();
        services.AddScoped<GameScoringDayRederiver>();
        services.AddScoped<IFavoriteRepository, EfFavoriteRepository>();
        services.AddScoped<IFavoriteService, FavoriteService>();
        return services;
    }

    /// <summary>Submissions, the hidden-scores rule with its signed screenshot URLs, and score maintenance.</summary>
    public static IServiceCollection AddSubmissionServices(this IServiceCollection services)
    {
        services.AddScoped<ISubmissionRepository, EfSubmissionRepository>();
        services.AddScoped<IUserSubmissionChecker, UserSubmissionChecker>();
        services.AddScoped<IScoreVisibilityService, ScoreVisibilityService>();
        services.AddSingleton<IScreenshotUrlSigner, ScreenshotUrlSigner>();
        services.AddScoped<ISubmissionQueryService, SubmissionQueryService>();
        services.AddScoped<ISubmissionCreationService, SubmissionCreationService>();
        services.AddScoped<ISubmissionScoreEditService, SubmissionScoreEditService>();
        services.AddScoped<ISubmissionDeletionService, SubmissionDeletionService>();
        services.AddScoped<SubmissionDraftBuilder>();
        services.AddScoped<FinalizedDayWinnerReconciler>();
        services.AddScoped<IScoreMaintenanceService, ScoreMaintenanceService>();
        return services;
    }

    /// <summary>Scoring-day finalization, including the background service that runs it.</summary>
    public static IServiceCollection AddScoringDayServices(this IServiceCollection services)
    {
        services.AddScoped<IScoringDayResultRepository, EfScoringDayResultRepository>();
        services.AddScoped<IScoringDayFinalizerService, ScoringDayFinalizerService>();
        services.AddScoped<ScoringDayOutcomeRecorder>();
        services.AddScoped<ScoringDayNotificationFactory>();
        services.AddHostedService<ScoringDayBackgroundService>();
        return services;
    }

    /// <summary>XP and levels. <see cref="XpConfig"/> is validated on start (see <see cref="XpConfigValidator"/>).</summary>
    public static IServiceCollection AddXpServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<XpConfig>()
            .Bind(configuration.GetSection(XpConfig.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<XpConfig>, XpConfigValidator>();
        services.AddSingleton(sp =>
        {
            var cfg = sp.GetRequiredService<IOptions<XpConfig>>().Value;
            return new LevelCalculator(cfg.LevelBase, cfg.LevelExponent);
        });
        services.AddScoped<IXpEventRepository, EfXpEventRepository>();
        services.AddScoped<IXpService, XpService>();
        return services;
    }

    public static IServiceCollection AddUserServices(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IUserProfileRepository, EfUserProfileRepository>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<INotificationRepository, EfNotificationRepository>();
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }

    public static IServiceCollection AddAchievementServices(this IServiceCollection services)
    {
        services.AddScoped<IUserAchievementRepository, EfUserAchievementRepository>();
        services.AddScoped<IAchievementService, AchievementService>();
        services.AddScoped<AchievementProgressReader>();
        return services;
    }

    public static IServiceCollection AddFriendServices(this IServiceCollection services)
    {
        services.AddScoped<IFriendRepository, EfFriendRepository>();
        services.AddScoped<IFriendRequestService, FriendRequestService>();
        services.AddScoped<IFriendshipService, FriendshipService>();
        return services;
    }

    public static IServiceCollection AddLeagueServices(this IServiceCollection services)
    {
        services.AddScoped<ILeagueRepository, EfLeagueRepository>();
        services.AddScoped<ILeagueInvitationRepository, EfLeagueInvitationRepository>();
        services.AddScoped<ILeagueStandingsRepository, EfLeagueStandingsRepository>();
        services.AddScoped<ILeagueService, LeagueService>();
        services.AddScoped<ILeagueInvitationService, LeagueInvitationService>();
        services.AddScoped<ILeagueStandingsService, LeagueStandingsService>();
        return services;
    }

    public static IServiceCollection AddAdminServices(this IServiceCollection services)
    {
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IAdminXpEventService, AdminXpEventService>();
        services.AddScoped<IAdminUserDeletionService, AdminUserDeletionService>();
        services.AddScoped<IUserDeletionRepository, EfUserDeletionRepository>();
        return services;
    }

    public static IServiceCollection AddDashboardServices(this IServiceCollection services)
    {
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<DashboardTodaySnapshotLoader>();
        services.AddScoped<DashboardFriendActivityBuilder>();
        return services;
    }

    /// <summary>The changelog (fetched over HTTP and cached) and the release info.</summary>
    public static IServiceCollection AddInfoServices(this IServiceCollection services)
    {
        services.AddHttpClient(ChangelogCache.ClientName, client => client.Timeout = ChangelogCache.Timeout);
        services.AddSingleton<ChangelogCache>();
        services.AddSingleton<ReleaseInfoReader>();
        services.AddSingleton<IInfoService, InfoService>();
        return services;
    }
}
