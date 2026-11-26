using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DailyChallenges.Data.Configurations;

internal sealed class LeagueInvitationConfiguration : IEntityTypeConfiguration<LeagueInvitation>
{
    public void Configure(EntityTypeBuilder<LeagueInvitation> invitation)
    {
        ConfigureRelationships(invitation);
        ConfigureLeagueLookup(invitation);
        ConfigureOnePendingDirectInvitePerInvitee(invitation);
        ConfigureUniqueInviteLinkTokens(invitation);
    }

    /// <summary>Deleting the league deletes its invitations; the inviter restricts; a deleted invitee becomes null.</summary>
    private static void ConfigureRelationships(EntityTypeBuilder<LeagueInvitation> invitation)
    {
        invitation.HasOne(i => i.League)
            .WithMany(l => l.Invitations)
            .HasForeignKey(i => i.LeagueId)
            .OnDelete(DeleteBehavior.Cascade);

        invitation.HasOne(i => i.Inviter)
            .WithMany()
            .HasForeignKey(i => i.InviterId)
            .OnDelete(DeleteBehavior.Restrict);

        invitation.HasOne(i => i.Invitee)
            .WithMany()
            .HasForeignKey(i => i.InviteeId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    /// <summary>Invitations of a league (FK lookups, cascade delete); the filtered pending-invite index can't serve these.</summary>
    private static void ConfigureLeagueLookup(EntityTypeBuilder<LeagueInvitation> invitation) =>
        invitation.HasIndex(i => i.LeagueId);

    /// <summary>At most one pending direct invite per league and invitee. Status 0 is <see cref="LeagueInvitationStatus.Pending"/>.</summary>
    private static void ConfigureOnePendingDirectInvitePerInvitee(EntityTypeBuilder<LeagueInvitation> invitation) =>
        invitation.HasIndex(i => new { i.LeagueId, i.InviteeId })
            .IsUnique()
            .HasFilter("[Status] = 0 AND [InviteeId] IS NOT NULL");

    /// <summary>Invite-link tokens are unique, and indexed for join-by-link lookups.</summary>
    private static void ConfigureUniqueInviteLinkTokens(EntityTypeBuilder<LeagueInvitation> invitation) =>
        invitation.HasIndex(i => i.Token)
            .IsUnique()
            .HasFilter("[Token] IS NOT NULL");
}
