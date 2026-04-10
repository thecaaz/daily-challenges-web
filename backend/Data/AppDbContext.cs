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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Game>()
                .HasMany(g => g.Submissions)
                .WithOne(s => s.Game!)
                .HasForeignKey(s => s.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            // allow linking submissions to users and ensure one submission per user per game
            modelBuilder.Entity<Submission>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // index submissions by game and user for lookup; uniqueness is now enforced per-game-day in the service layer
            modelBuilder.Entity<Submission>()
                .HasIndex(s => new { s.GameId, s.UserId });

            // index for queries ordering/filtering by creation time per game
            modelBuilder.Entity<Submission>()
                .HasIndex(s => new { s.GameId, s.CreatedAt });

            // index submissions by game and scoring day for fast available-dates queries
            modelBuilder.Entity<Submission>()
                .HasIndex(s => new { s.GameId, s.ScoringDay });

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

            modelBuilder.Entity<Favorite>()
                .HasIndex(f => new { f.UserId, f.GameId })
                .IsUnique();

            modelBuilder.Entity<Favorite>()
                .HasIndex(f => f.UserId);
        }
    }
}
