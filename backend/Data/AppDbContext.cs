using Microsoft.EntityFrameworkCore;
using DailyChallenges.Models;

namespace DailyChallenges.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Game> Games => Set<Game>();
        public DbSet<Submission> Submissions => Set<Submission>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Game>()
                .HasMany(g => g.Submissions)
                .WithOne(s => s.Game!)
                .HasForeignKey(s => s.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
