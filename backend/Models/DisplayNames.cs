namespace DailyChallenges.Models;

/// <summary>Names shown in API responses and notification texts when a user's name is missing.</summary>
public static class DisplayNames
{
    /// <summary>A submission stored without a username.</summary>
    public const string Anonymous = "Anonymous";

    /// <summary>The actor of a notification whose account could not be loaded.</summary>
    public const string Someone = "Someone";

    /// <summary>A league standings row whose submission and user both lack a name.</summary>
    public const string Unknown = "Unknown";
}
