using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DailyChallenges.Data.Configurations;

/// <summary>
/// One favorite per user per game, deleted with either. The unique index leads with UserId, so it also
/// serves "favorites of user X" lookups.
/// </summary>
internal sealed class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> favorite)
    {
        favorite.HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        favorite.HasOne(f => f.Game)
            .WithMany()
            .HasForeignKey(f => f.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        favorite.HasIndex(f => new { f.UserId, f.GameId })
            .IsUnique();
    }
}
