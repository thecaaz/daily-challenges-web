namespace DailyChallenges.Models;

/// <summary>
/// <see cref="Notification.Type"/> discriminator values (stored in the database). The frontend switches on
/// them, so they are part of the API.
/// </summary>
public static class NotificationTypes
{
    public const string DayWin = "day_win";
    public const string DayPlacement = "day_placement";
    public const string FriendRequest = "friend_request";
    public const string FriendRequestAccepted = "friend_request_accepted";
    public const string Achievement = "achievement";
}
