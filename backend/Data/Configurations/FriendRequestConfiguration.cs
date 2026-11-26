using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DailyChallenges.Data.Configurations;

/// <summary>
/// The (SenderId, ReceiverId) index serves outgoing-request lookups and the SenderId FK;
/// the (ReceiverId, Status) index serves incoming pending requests.
/// </summary>
internal sealed class FriendRequestConfiguration : IEntityTypeConfiguration<FriendRequest>
{
    private const string UserLowId = "UserLowId";
    private const string UserHighId = "UserHighId";

    public void Configure(EntityTypeBuilder<FriendRequest> friendRequest)
    {
        ConfigureParticipants(friendRequest);
        friendRequest.HasIndex(fr => new { fr.SenderId, fr.ReceiverId }).IsUnique();
        ConfigureOneRequestPerUserPair(friendRequest);
        friendRequest.HasIndex(fr => new { fr.ReceiverId, fr.Status });
    }

    /// <summary>The sender FK cascades on delete; the receiver FK restricts, to avoid multiple cascade paths.</summary>
    private static void ConfigureParticipants(EntityTypeBuilder<FriendRequest> friendRequest)
    {
        friendRequest.HasOne(fr => fr.Sender)
            .WithMany()
            .HasForeignKey(fr => fr.SenderId)
            .OnDelete(DeleteBehavior.Cascade);

        friendRequest.HasOne(fr => fr.Receiver)
            .WithMany()
            .HasForeignKey(fr => fr.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>
    /// Only one request row between any two users, in either direction: the shadow, virtual computed columns
    /// UserLowId / UserHighId hold the ordered pair, and their unique index rejects B->A while A->B exists.
    /// </summary>
    private static void ConfigureOneRequestPerUserPair(EntityTypeBuilder<FriendRequest> friendRequest)
    {
        friendRequest.Property<int>(UserLowId)
            .HasComputedColumnSql("min([SenderId], [ReceiverId])", stored: false);

        friendRequest.Property<int>(UserHighId)
            .HasComputedColumnSql("max([SenderId], [ReceiverId])", stored: false);

        friendRequest.HasIndex(UserLowId, UserHighId)
            .IsUnique();
    }
}
