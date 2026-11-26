using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DailyChallenges.Data.Configurations;

/// <summary>
/// One result per game and scoring day. Deleting the game deletes its results; deleting the winner
/// keeps the result with a null winner.
/// </summary>
internal sealed class ScoringDayResultConfiguration : IEntityTypeConfiguration<ScoringDayResult>
{
    public void Configure(EntityTypeBuilder<ScoringDayResult> result)
    {
        result.HasOne(r => r.Game)
            .WithMany()
            .HasForeignKey(r => r.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        result.HasOne(r => r.WinnerUser)
            .WithMany()
            .HasForeignKey(r => r.WinnerUserId)
            .OnDelete(DeleteBehavior.SetNull);

        result.HasIndex(r => new { r.GameId, r.ScoringDay })
            .IsUnique();
    }
}
