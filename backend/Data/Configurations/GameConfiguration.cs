using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DailyChallenges.Data.Configurations;

/// <summary>Deleting a game deletes its submissions.</summary>
internal sealed class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> game) =>
        game.HasMany(g => g.Submissions)
            .WithOne(s => s.Game!)
            .HasForeignKey(s => s.GameId)
            .OnDelete(DeleteBehavior.Cascade);
}
