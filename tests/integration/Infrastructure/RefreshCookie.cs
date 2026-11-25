namespace DailyChallenges.IntegrationTests.Infrastructure;

/// <summary>Reads the <c>refresh_token</c> cookie the auth endpoints set.</summary>
public static class RefreshCookie
{
    private const string Prefix = "refresh_token=";

    /// <summary>The refresh_token value from a response's Set-Cookie header (null when absent or cleared).</summary>
    public static string? ValueFrom(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies)) return null;
        var cookie = cookies.FirstOrDefault(c => c.StartsWith(Prefix, StringComparison.Ordinal));
        if (cookie is null) return null;
        var value = cookie[Prefix.Length..].Split(';')[0];
        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>The whole refresh_token Set-Cookie header, attributes included.</summary>
    public static string SetCookieHeaderFrom(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(Prefix, StringComparison.Ordinal));
}
