using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DailyChallenges.Data.Configurations;

/// <summary>The owner FK restricts deletes, so deleting a user never cascades into a league by accident.</summary>
internal sealed class LeagueConfiguration : IEntityTypeConfiguration<League>
{
    public void Configure(EntityTypeBuilder<League> league) =>
        league.HasOne(l => l.Owner)
            .WithMany()
            .HasForeignKey(l => l.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
}
