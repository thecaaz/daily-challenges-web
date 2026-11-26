using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DailyChallenges.Data.Configurations;

/// <summary>Each achievement is unlocked at most once per user; deleting the user deletes their achievements.</summary>
internal sealed class UserAchievementConfiguration : IEntityTypeConfiguration<UserAchievement>
{
    public void Configure(EntityTypeBuilder<UserAchievement> userAchievement)
    {
        userAchievement.HasOne(ua => ua.User)
            .WithMany()
            .HasForeignKey(ua => ua.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        userAchievement.HasIndex(ua => new { ua.UserId, ua.AchievementId })
            .IsUnique();
    }
}
