using Microsoft.EntityFrameworkCore;
using DailyChallenges.Models;

namespace DailyChallenges.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Usernames are unique case-insensitively: NOCASE on the column makes both the unique index
            // and plain `Username == x` lookups ignore ASCII case.
            modelBuilder.Entity<User>()
                .Property(u => u.Username)
                .UseCollation("NOCASE");

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            // Revoked refresh tokens: the purge deletes by expiry.
            modelBuilder.Entity<RevokedRefreshToken>()
                .HasIndex(t => t.ExpiresAt);

            modelBuilder.Entity<Game>()
                .HasMany(g => g.Submissions)
                .WithOne(s => s.Game!)
                .HasForeignKey(s => s.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            // link submissions to users; deleting a user keeps their submissions (UserId set to null)
            modelBuilder.Entity<Submission>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // index submissions by game and user for lookup (not unique: one row per user per game per day, see below)
            modelBuilder.Entity<Submission>()
                .HasIndex(s => new { s.GameId, s.UserId });

            // index for queries ordering/filtering by creation time per game
            modelBuilder.Entity<Submission>()
                .HasIndex(s => new { s.GameId, s.CreatedAt });

            // index submissions by game and scoring day for fast available-dates queries
            modelBuilder.Entity<Submission>()
                .HasIndex(s => new { s.GameId, s.ScoringDay });

            // one submission per authenticated user per game per scoring day (race-condition proof)
            modelBuilder.Entity<Submission>()
                .HasIndex(s => new { s.UserId, s.GameId, s.ScoringDay })
                .IsUnique()
                .HasFilter("[UserId] IS NOT NULL");

            // XpEvents: FK to User (required), FK to Submission and Game (optional)
            modelBuilder.Entity<XpEvent>()
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<XpEvent>()
                .HasOne(e => e.Submission)
                .WithMany()
                .HasForeignKey(e => e.SubmissionId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<XpEvent>()
                .HasOne(e => e.Game)
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.SetNull);

            // Index for fetching all XP history for a user efficiently
            modelBuilder.Entity<XpEvent>()
                .HasIndex(e => new { e.UserId, e.CreatedAt });

            // At most one day-win award per user, game and scoring day (makes finalization idempotent)
            modelBuilder.Entity<XpEvent>()
                .HasIndex(e => new { e.UserId, e.GameId, e.ScoringDay })
                .IsUnique()
                .HasFilter("[EventType] = 'day_win'")
                .HasDatabaseName("IX_XpEvents_UserId_GameId_ScoringDay_DayWin");

            // Notifications: FK to User (required), FK to Game (optional)
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Game)
                .WithMany()
                .HasForeignKey(n => n.GameId)
                .OnDelete(DeleteBehavior.SetNull);

            // The user who caused the notification (friend/league actions). Null for system notifications.
            modelBuilder.Entity<Notification>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(n => n.ActorUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });

            // ScoringDayResults: FK to Game (cascade), FK to WinnerUser (set null)
            modelBuilder.Entity<ScoringDayResult>()
                .HasOne(r => r.Game)
                .WithMany()
                .HasForeignKey(r => r.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ScoringDayResult>()
                .HasOne(r => r.WinnerUser)
                .WithMany()
                .HasForeignKey(r => r.WinnerUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ScoringDayResult>()
                .HasIndex(r => new { r.GameId, r.ScoringDay })
                .IsUnique();

            // Favorites: per-user favorites for games
            modelBuilder.Entity<Favorite>()
                .HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Favorite>()
                .HasOne(f => f.Game)
                .WithMany()
                .HasForeignKey(f => f.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            // One favorite per user per game; also serves "favorites of user X" lookups (UserId is the leading column).
            modelBuilder.Entity<Favorite>()
                .HasIndex(f => new { f.UserId, f.GameId })
                .IsUnique();

            // FriendRequests: Sender FK cascades on delete; Receiver FK restricts to avoid multiple cascade paths
            modelBuilder.Entity<FriendRequest>()
                .HasOne(fr => fr.Sender)
                .WithMany()
                .HasForeignKey(fr => fr.SenderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FriendRequest>()
                .HasOne(fr => fr.Receiver)
                .WithMany()
                .HasForeignKey(fr => fr.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            // Outgoing-request lookups (and the SenderId FK)
            modelBuilder.Entity<FriendRequest>()
                .HasIndex(fr => new { fr.SenderId, fr.ReceiverId })
                .IsUnique();

            // Only one request record between any two users, in either direction: virtual computed columns hold
            // the ordered pair, and a unique index on them rejects B->A while A->B exists.
            modelBuilder.Entity<FriendRequest>()
                .Property<int>("UserLowId")
                .HasComputedColumnSql("min([SenderId], [ReceiverId])", stored: false);

            modelBuilder.Entity<FriendRequest>()
                .Property<int>("UserHighId")
                .HasComputedColumnSql("max([SenderId], [ReceiverId])", stored: false);

            modelBuilder.Entity<FriendRequest>()
                .HasIndex("UserLowId", "UserHighId")
                .IsUnique();

            // Fast query for incoming pending requests
            modelBuilder.Entity<FriendRequest>()
                .HasIndex(fr => new { fr.ReceiverId, fr.Status });

            // UserAchievements: FK to User (cascade on delete), unique per user+achievement
            modelBuilder.Entity<UserAchievement>()
                .HasOne(ua => ua.User)
                .WithMany()
                .HasForeignKey(ua => ua.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserAchievement>()
                .HasIndex(ua => new { ua.UserId, ua.AchievementId })
                .IsUnique();

            // Leagues: owner FK (restrict to prevent accidental cascade)
            modelBuilder.Entity<League>()
                .HasOne(l => l.Owner)
                .WithMany()
                .HasForeignKey(l => l.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            // LeagueMembers: FK to League (cascade) and User (cascade)
            modelBuilder.Entity<LeagueMember>()
                .HasOne(m => m.League)
                .WithMany(l => l.Members)
                .HasForeignKey(m => m.LeagueId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LeagueMember>()
                .HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // One membership row per user per league
            modelBuilder.Entity<LeagueMember>()
                .HasIndex(m => new { m.LeagueId, m.UserId })
                .IsUnique();

            // Fast lookup: all leagues a user belongs to
            modelBuilder.Entity<LeagueMember>()
                .HasIndex(m => m.UserId);

            // LeagueInvitations: FK to League (cascade), Inviter (restrict), Invitee (set null on delete)
            modelBuilder.Entity<LeagueInvitation>()
                .HasOne(i => i.League)
                .WithMany(l => l.Invitations)
                .HasForeignKey(i => i.LeagueId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LeagueInvitation>()
                .HasOne(i => i.Inviter)
                .WithMany()
                .HasForeignKey(i => i.InviterId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<LeagueInvitation>()
                .HasOne(i => i.Invitee)
                .WithMany()
                .HasForeignKey(i => i.InviteeId)
                .OnDelete(DeleteBehavior.SetNull);

            // Invitations of a league (FK lookups, cascade delete); the filtered index below can't serve these.
            modelBuilder.Entity<LeagueInvitation>()
                .HasIndex(i => i.LeagueId);

            // At most one pending direct invite per (league, invitee). Status 0 = LeagueInvitationStatus.Pending.
            modelBuilder.Entity<LeagueInvitation>()
                .HasIndex(i => new { i.LeagueId, i.InviteeId })
                .IsUnique()
                .HasFilter("[Status] = 0 AND [InviteeId] IS NOT NULL");

            // Invite-link tokens are unique (and indexed for join-by-link lookups)
            modelBuilder.Entity<LeagueInvitation>()
                .HasIndex(i => i.Token)
                .IsUnique()
                .HasFilter("[Token] IS NOT NULL");
        }
    }
}
