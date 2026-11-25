namespace DailyChallenges.Models;

/// <summary>The known <see cref="XpEvent.EventType"/> discriminator values (stored in the database).</summary>
public static class XpEventTypes
{
    public const string Submission = "submission";
    public const string DayWin = "day_win";
    public const string AdminAdjustment = "admin_adjustment";
    public const string SubmissionRevoked = "submission_revoked";
}
