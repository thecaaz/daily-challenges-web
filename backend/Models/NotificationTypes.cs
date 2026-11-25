namespace DailyChallenges.Models;

/// <summary>
/// <see cref="Notification.Type"/> discriminator values written by the scoring-day finalizer and the
/// achievement service (stored in the database).
/// </summary>
public static class NotificationTypes
{
    public const string DayWin = "day_win";
    public const string DayPlacement = "day_placement";
    public const string Achievement = "achievement";
}
