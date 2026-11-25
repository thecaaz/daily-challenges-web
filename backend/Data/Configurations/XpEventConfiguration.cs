using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DailyChallenges.Data.Configurations;

/// <summary>Indexed by user and creation time for a user's XP history.</summary>
internal sealed class XpEventConfiguration : IEntityTypeConfiguration<XpEvent>
{
    public void Configure(EntityTypeBuilder<XpEvent> xpEvent)
    {
        ConfigureRelationships(xpEvent);
        xpEvent.HasIndex(e => new { e.UserId, e.CreatedAt });
        ConfigureOneDayWinPerUserGameAndDay(xpEvent);
    }

    /// <summary>The user is required (cascade); the submission and game are optional and set to null when deleted.</summary>
    private static void ConfigureRelationships(EntityTypeBuilder<XpEvent> xpEvent)
    {
        xpEvent.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        xpEvent.HasOne(e => e.Submission)
            .WithMany()
            .HasForeignKey(e => e.SubmissionId)
            .OnDelete(DeleteBehavior.SetNull);

        xpEvent.HasOne(e => e.Game)
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    /// <summary>At most one day-win award per user, game and scoring day, which makes finalization idempotent.</summary>
    private static void ConfigureOneDayWinPerUserGameAndDay(EntityTypeBuilder<XpEvent> xpEvent) =>
        xpEvent.HasIndex(e => new { e.UserId, e.GameId, e.ScoringDay })
            .IsUnique()
            .HasFilter("[EventType] = 'day_win'")
            .HasDatabaseName("IX_XpEvents_UserId_GameId_ScoringDay_DayWin");
}
