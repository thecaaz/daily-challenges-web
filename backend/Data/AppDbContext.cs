using Microsoft.EntityFrameworkCore;
using DailyChallenges.Data.Configurations;
using DailyChallenges.Models;

namespace DailyChallenges.Data;

/// <summary>
/// The EF Core context. Each entity's keys, relationships and indexes live in its
/// <see cref="IEntityTypeConfiguration{TEntity}"/> under <c>Data/Configurations</c>.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<User> Users => Set<User>();
    public DbSet<XpEvent> XpEvents => Set<XpEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ScoringDayResult> ScoringDayResults => Set<ScoringDayResult>();
    public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<League> Leagues => Set<League>();
    public DbSet<LeagueMember> LeagueMembers => Set<LeagueMember>();
    public DbSet<LeagueInvitation> LeagueInvitations => Set<LeagueInvitation>();
    public DbSet<RevokedRefreshToken> RevokedRefreshTokens => Set<RevokedRefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder
            .ApplyConfiguration(new UserConfiguration())
            .ApplyConfiguration(new RevokedRefreshTokenConfiguration())
            .ApplyConfiguration(new GameConfiguration())
            .ApplyConfiguration(new SubmissionConfiguration())
            .ApplyConfiguration(new XpEventConfiguration())
            .ApplyConfiguration(new NotificationConfiguration())
            .ApplyConfiguration(new ScoringDayResultConfiguration())
            .ApplyConfiguration(new FavoriteConfiguration())
            .ApplyConfiguration(new FriendRequestConfiguration())
            .ApplyConfiguration(new UserAchievementConfiguration())
            .ApplyConfiguration(new LeagueConfiguration())
            .ApplyConfiguration(new LeagueMemberConfiguration())
            .ApplyConfiguration(new LeagueInvitationConfiguration());
}
