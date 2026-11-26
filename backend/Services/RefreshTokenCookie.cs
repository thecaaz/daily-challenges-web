using System.Diagnostics.CodeAnalysis;

namespace DailyChallenges.Services;

/// <summary>
/// The refresh token travels only in this HttpOnly, SameSite=Strict cookie (Secure everywhere except
/// Development). The access token is never a cookie; clients send it as a Bearer header.
/// </summary>
public sealed class RefreshTokenCookie(IWebHostEnvironment environment)
{
    public const string Name = "refresh_token";

    public bool TryRead(HttpRequest request, [NotNullWhen(true)] out string? token) =>
        request.Cookies.TryGetValue(Name, out token);

    public void Write(HttpResponse response, IssuedToken token) =>
        response.Cookies.Append(Name, token.Value, BuildOptions(token.ExpiresAt));

    public void Delete(HttpResponse response) =>
        response.Cookies.Delete(Name, BuildOptions(expires: null));

    private CookieOptions BuildOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Expires = expires
    };
}
