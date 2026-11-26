namespace DailyChallenges.Services;

/// <summary>The signed-in user a request acts for, and whether they are an admin.</summary>
public sealed record RequestingUser(int UserId, bool IsAdmin);
