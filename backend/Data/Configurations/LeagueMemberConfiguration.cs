using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DailyChallenges.Data.Configurations;

/// <summary>
/// One membership row per user per league, deleted with either the league or the user.
/// The UserId index serves "all leagues a user belongs to".
/// </summary>
internal sealed class LeagueMemberConfiguration : IEntityTypeConfiguration<LeagueMember>
{
    public void Configure(EntityTypeBuilder<LeagueMember> member)
    {
        member.HasOne(m => m.League)
            .WithMany(l => l.Members)
            .HasForeignKey(m => m.LeagueId)
            .OnDelete(DeleteBehavior.Cascade);

        member.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        member.HasIndex(m => new { m.LeagueId, m.UserId })
            .IsUnique();

        member.HasIndex(m => m.UserId);
    }
}
