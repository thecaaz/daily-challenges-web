namespace DailyChallenges.Controllers;

/// <summary>Response content types the controllers set explicitly.</summary>
internal static class ContentTypes
{
    /// <summary>Fallback for a stored blob without a known content type.</summary>
    public const string OctetStream = "application/octet-stream";
    public const string CsvUtf8 = "text/csv; charset=utf-8";
}
