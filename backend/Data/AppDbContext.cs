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

            modelBuilder.Entity<Submission>()
                .HasIndex(s => new { s.GameId, s.UserId })
                .IsUnique();
        }
    }
}
