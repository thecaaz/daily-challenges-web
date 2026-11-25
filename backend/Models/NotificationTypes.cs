namespace DailyChallenges.Models;

/// <summary>Values of <see cref="Notification.Type"/>; the frontend switches on them, so they are part of the API.</summary>
public static class NotificationTypes
{
    public const string DayWin = "day_win";
    public const string DayPlacement = "day_placement";
    public const string FriendRequest = "friend_request";
    public const string FriendRequestAccepted = "friend_request_accepted";
}
