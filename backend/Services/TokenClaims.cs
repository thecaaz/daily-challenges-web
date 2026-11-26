namespace DailyChallenges.Services;

/// <summary>Custom JWT claim names and values.</summary>
public static class TokenClaims
{
    /// <summary>"access" for Bearer tokens, "refresh" for the refresh cookie. Bearer auth accepts only "access".</summary>
    public const string TokenUse = "token_use";
    /// <summary>The user's <see cref="Models.User.TokenVersion"/> when the token was issued.</summary>
    public const string TokenVersion = "tv";

    public const string Access = "access";
    public const string Refresh = "refresh";
}
