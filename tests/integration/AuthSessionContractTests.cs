using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;
using DailyChallenges.IntegrationTests.Infrastructure;
using DailyChallenges.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static DailyChallenges.IntegrationTests.Infrastructure.HttpResponseJson;

namespace DailyChallenges.IntegrationTests;

/// <summary>
/// Pins the wire contract of login / refresh / logout / me: token claims, lifetimes, refresh cookie
/// attributes and the error bodies. Tests that depend on "first user is admin" own their database.
/// </summary>
public class AuthSessionContractTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthSessionContractTests(ApiFactory factory) => _factory = factory;

    private const string Password = "correct-horse-1";
    private static readonly TimeSpan SecondPrecision = TimeSpan.FromSeconds(1);

    private static string RefreshCookieValue(HttpResponseMessage res) =>
        RefreshCookie.SetCookieHeaderFrom(res)["refresh_token=".Length..].Split(';')[0];

    private static async Task<HttpResponseMessage> RegisterAndLoginAsync(ApiFactory factory, string username)
    {
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateCookielessClient().PostAsJsonAsync("/api/auth/register", new { username, password = Password })).StatusCode);
        var res = await factory.CreateCookielessClient().PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return res;
    }

    private static string? Claim(JwtSecurityToken token, string type) =>
        token.Claims.FirstOrDefault(c => c.Type == type)?.Value;

    /// <summary>Asserts <paramref name="actual"/> is <paramref name="lifetime"/> after some instant in [before, after].</summary>
    private static void AssertExpiresAfter(TimeSpan lifetime, DateTimeOffset before, DateTimeOffset after, DateTimeOffset actual) =>
        Assert.InRange(actual, before + lifetime - SecondPrecision, after + lifetime + SecondPrecision);

    [Fact]
    public async Task Register_makes_the_first_user_admin_and_later_users_not()
    {
        using var factory = new ApiFactory();

        var first = await factory.CreateCookielessClient().PostAsJsonAsync("/api/auth/register", new { username = "first-user", password = Password });
        var second = await factory.CreateCookielessClient().PostAsJsonAsync("/api/auth/register", new { username = "second-user", password = Password });

        Assert.True((await ReadJsonAsync(first)).GetProperty("isAdmin").GetBoolean());
        Assert.False((await ReadJsonAsync(second)).GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task Login_access_token_has_the_session_claims_and_a_30_minute_lifetime()
    {
        using var factory = new ApiFactory();
        var jwt = factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var before = DateTimeOffset.UtcNow;

        var res = await RegisterAndLoginAsync(factory, "claims-admin");
        var after = DateTimeOffset.UtcNow;

        var body = await ReadJsonAsync(res);
        Assert.Equal(1800, body.GetProperty("expiresInSeconds").GetInt32());
        var access = new JwtSecurityTokenHandler().ReadJwtToken(body.GetProperty("accessToken").GetString());
        Assert.Equal("HS256", access.Header.Alg);
        Assert.Equal(jwt.Issuer, access.Issuer);
        Assert.Equal(jwt.Audience, Assert.Single(access.Audiences));
        Assert.Equal(body.GetProperty("user").GetProperty("id").GetInt32().ToString(), Claim(access, ClaimTypes.NameIdentifier));
        Assert.Equal("claims-admin", Claim(access, ClaimTypes.Name));
        Assert.Equal("access", Claim(access, "token_use"));
        Assert.Equal("0", Claim(access, "tv"));
        Assert.Equal("Admin", Claim(access, ClaimTypes.Role));
        Assert.Null(Claim(access, "jti"));
        AssertExpiresAfter(TimeSpan.FromMinutes(30), before, after, access.ValidTo);
    }

    [Fact]
    public async Task Login_refresh_token_has_a_jti_and_the_configured_lifetime_and_no_admin_role_for_regular_users()
    {
        var refreshDays = _factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value.RefreshExpiresDays;
        await RegisterAndLoginAsync(_factory, "someone-first");
        var before = DateTimeOffset.UtcNow;

        var res = await RegisterAndLoginAsync(_factory, "regular-user");
        var after = DateTimeOffset.UtcNow;

        var refresh = new JwtSecurityTokenHandler().ReadJwtToken(RefreshCookieValue(res));
        Assert.Equal("refresh", Claim(refresh, "token_use"));
        Assert.Equal("regular-user", Claim(refresh, ClaimTypes.Name));
        Assert.Equal("0", Claim(refresh, "tv"));
        Assert.Null(Claim(refresh, ClaimTypes.Role));
        Assert.Matches(new Regex("^[0-9a-f]{32}$"), refresh.Id);
        Assert.Equal(30, refreshDays);
        AssertExpiresAfter(TimeSpan.FromDays(refreshDays), before, after, refresh.ValidTo);
        var access = new JwtSecurityTokenHandler().ReadJwtToken((await ReadJsonAsync(res)).GetProperty("accessToken").GetString());
        Assert.Null(Claim(access, ClaimTypes.Role));
    }

    [Fact]
    public async Task Login_sets_a_strict_secure_http_only_refresh_cookie_for_the_refresh_lifetime()
    {
        var before = DateTimeOffset.UtcNow;

        var cookie = RefreshCookie.SetCookieHeaderFrom(await RegisterAndLoginAsync(_factory, "cookie-user")).ToLowerInvariant();
        var after = DateTimeOffset.UtcNow;

        Assert.Contains("; path=/", cookie);
        Assert.Contains("; secure", cookie);
        Assert.Contains("; samesite=strict", cookie);
        Assert.Contains("; httponly", cookie);
        var expires = DateTimeOffset.Parse(Regex.Match(cookie, "expires=([^;]+)").Groups[1].Value);
        AssertExpiresAfter(TimeSpan.FromDays(30), before, after, expires);
    }

    [Fact]
    public async Task Refresh_issues_a_new_access_token_and_cookie()
    {
        var login = await RegisterAndLoginAsync(_factory, "refresh-user");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"refresh_token={RefreshCookieValue(login)}");

        var res = await _factory.CreateCookielessClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await ReadJsonAsync(res);
        Assert.Equal("refresh-user", body.GetProperty("user").GetProperty("username").GetString());
        Assert.Equal(1800, body.GetProperty("expiresInSeconds").GetInt32());
        Assert.Contains("; samesite=strict", RefreshCookie.SetCookieHeaderFrom(res).ToLowerInvariant());
    }

    [Fact]
    public async Task Logout_expires_the_refresh_cookie_with_the_same_attributes()
    {

        var res = await _factory.CreateCookielessClient().PostAsync("/api/auth/logout", null);

        var body = await ReadJsonAsync(res);
        Assert.True(body.GetProperty("success").GetBoolean());
        var cookie = RefreshCookie.SetCookieHeaderFrom(res).ToLowerInvariant();
        Assert.StartsWith("refresh_token=;", cookie);
        Assert.Contains("expires=thu, 01 jan 1970 00:00:00 gmt", cookie);
        Assert.Contains("; secure", cookie);
        Assert.Contains("; samesite=strict", cookie);
        Assert.Contains("; httponly", cookie);
    }

    [Fact]
    public async Task Refresh_without_a_cookie_is_401_with_the_session_expired_message()
    {

        var res = await _factory.CreateCookielessClient().PostAsync("/api/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Equal("Session expired. Please log in again.", (await ReadJsonAsync(res)).GetProperty("message").GetString());
    }

    [Fact]
    public async Task Refresh_with_a_garbage_cookie_is_401()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", "refresh_token=not-a-jwt");

        var res = await _factory.CreateCookielessClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Me_returns_the_caller_and_401_without_a_token_or_for_an_unknown_user()
    {
        var login = await ReadJsonAsync(await RegisterAndLoginAsync(_factory, "me-user"));
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new("Bearer", login.GetProperty("accessToken").GetString());

        var me = await _factory.CreateCookielessClient().SendAsync(request);
        var anonymous = await _factory.CreateCookielessClient().GetAsync("/api/auth/me");
        var unknown = await _factory.CreateAuthenticatedClient(999_999, "ghost").GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal("me-user", (await ReadJsonAsync(me)).GetProperty("username").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
    }

    [Fact]
    public async Task Profile_endpoints_return_404_for_unknown_users_and_401_for_anonymous_me()
    {

        var unknown = await _factory.CreateCookielessClient().GetAsync("/api/users/999999/profile");
        var anonymousMe = await _factory.CreateCookielessClient().GetAsync("/api/users/me/profile");

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousMe.StatusCode);
    }
}
