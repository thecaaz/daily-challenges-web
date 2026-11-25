namespace DailyChallenges.Mapping;

/// <summary>URL of a game's image endpoint; null when the game has no stored image.</summary>
public static class GameImageUrl
{
    public static string? For(int gameId, string? screenshotContentType) =>
        string.IsNullOrEmpty(screenshotContentType) ? null : $"/api/games/{gameId}/image";
}
