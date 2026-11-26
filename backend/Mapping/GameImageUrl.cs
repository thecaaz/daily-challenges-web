namespace DailyChallenges.Mapping;

/// <summary>URL of a game's image endpoint.</summary>
public static class GameImageUrl
{
    public static string Of(int gameId) => $"/api/games/{gameId}/image";

    /// <summary>Null when the game has no stored image.</summary>
    public static string? For(int gameId, string? screenshotContentType) =>
        string.IsNullOrEmpty(screenshotContentType) ? null : Of(gameId);
}
