using Microsoft.EntityFrameworkCore;
using DailyChallenges.Models;

namespace DailyChallenges.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Game> Games => Set<Game>();
        public DbSet<Submission> Submissions => Set<Submission>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Models.XpEvent> XpEvents => Set<Models.XpEvent>();

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
            modelBuilder.Entity<Models.XpEvent>()
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Models.XpEvent>()
                .HasOne(e => e.Submission)
                .WithMany()
                .HasForeignKey(e => e.SubmissionId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Models.XpEvent>()
                .HasOne(e => e.Game)
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.SetNull);

            // Index for fetching all XP history for a user efficiently
            modelBuilder.Entity<Models.XpEvent>()
                .HasIndex(e => new { e.UserId, e.CreatedAt });
        }
    }
}
