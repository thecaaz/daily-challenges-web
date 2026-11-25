using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DailyChallenges.Data.Configurations;

internal sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> submission)
    {
        ConfigureUserLink(submission);
        ConfigureLookupIndexes(submission);
        ConfigureOneSubmissionPerUserPerGamePerDay(submission);
    }

    /// <summary>Deleting a user keeps their submissions (UserId set to null).</summary>
    private static void ConfigureUserLink(EntityTypeBuilder<Submission> submission) =>
        submission.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.SetNull);

    /// <summary>
    /// Lookups by game and user (not unique: one row per user per game per day), listings ordered by
    /// creation time per game, and available-dates queries by game and scoring day.
    /// </summary>
    private static void ConfigureLookupIndexes(EntityTypeBuilder<Submission> submission)
    {
        submission.HasIndex(s => new { s.GameId, s.UserId });
        submission.HasIndex(s => new { s.GameId, s.CreatedAt });
        submission.HasIndex(s => new { s.GameId, s.ScoringDay });
    }

    /// <summary>One submission per signed-in user per game per scoring day, enforced by the database so it is race-proof.</summary>
    private static void ConfigureOneSubmissionPerUserPerGamePerDay(EntityTypeBuilder<Submission> submission) =>
        submission.HasIndex(s => new { s.UserId, s.GameId, s.ScoringDay })
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL");
}
