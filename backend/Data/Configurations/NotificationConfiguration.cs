using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DailyChallenges.Data.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> notification)
    {
        ConfigureRecipientAndGame(notification);
        ConfigureActor(notification);
        notification.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
    }

    /// <summary>The recipient is required (cascade); the game is optional and set to null when deleted.</summary>
    private static void ConfigureRecipientAndGame(EntityTypeBuilder<Notification> notification)
    {
        notification.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        notification.HasOne(n => n.Game)
            .WithMany()
            .HasForeignKey(n => n.GameId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    /// <summary>The user who caused the notification (friend and league actions); null for system notifications.</summary>
    private static void ConfigureActor(EntityTypeBuilder<Notification> notification) =>
        notification.HasOne<User>()
            .WithMany()
            .HasForeignKey(n => n.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);
}
