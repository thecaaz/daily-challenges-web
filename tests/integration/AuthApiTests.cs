using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DailyChallenges.IntegrationTests.Infrastructure;
using DailyChallenges.Models;
using DailyChallenges.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DailyChallenges.IntegrationTests;

/// <summary>Register / login / refresh / logout through the real pipeline: token types, rotation and revocation.</summary>
public class AuthApiTests : IClassFixture<ApiFactory>
{
    private const string Password = "correct-horse-1";
    private readonly ApiFactory _factory;

    public AuthApiTests(ApiFactory factory) => _factory = factory;

    private HttpClient Client() => _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    /// <summary>The refresh_token value from a response's Set-Cookie header (null when absent or cleared).</summary>
    private static string? RefreshCookie(HttpResponseMessage res)
    {
        if (!res.Headers.TryGetValues("Set-Cookie", out var cookies)) return null;
        foreach (var c in cookies)
        {
            if (!c.StartsWith("refresh_token=", StringComparison.Ordinal)) continue;
            var value = c["refresh_token=".Length..].Split(';')[0];
            return string.IsNullOrEmpty(value) ? null : value;
        }
        return null;
    }

    private async Task RegisterAsync(string username, string password = Password)
    {
        var res = await Client().PostAsJsonAsync("/api/auth/register", new { username, password });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    private async Task<(string AccessToken, string RefreshToken, int UserId)> LoginAsync(string username, string password = Password)
    {
        var res = await Client().PostAsJsonAsync("/api/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await ReadJsonAsync(res);
        var refresh = RefreshCookie(res);
        Assert.NotNull(refresh);
        return (json.GetProperty("accessToken").GetString()!, refresh!, json.GetProperty("user").GetProperty("id").GetInt32());
    }

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        req.Headers.Add("Cookie", $"refresh_token={refreshToken}");
        return Client().SendAsync(req);
    }

    private Task<HttpResponseMessage> GetWithBearerAsync(string token, string url = "/api/notifications/unread-count")
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new("Bearer", token);
        return Client().SendAsync(req);
    }

    [Theory]
    [InlineData("ab", Password)]                     // username too short
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456", Password)] // 33 chars
    [InlineData("bad name", Password)]               // space inside
    [InlineData("emoji😀", Password)]                // outside [A-Za-z0-9_.-]
    [InlineData("validname", "short")]               // password too short
    public async Task Register_rejects_invalid_credentials_with_400_message(string username, string password)
    {
        var res = await Client().PostAsJsonAsync("/api/auth/register", new { username, password });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.False(string.IsNullOrEmpty((await ReadJsonAsync(res)).GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Register_rejects_password_longer_than_128()
    {
        var res = await Client().PostAsJsonAsync("/api/auth/register", new { username = "longpass", password = new string('x', 129) });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var ok = await Client().PostAsJsonAsync("/api/auth/register", new { username = "longpass", password = new string('x', 128) });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task Username_is_trimmed_and_unique_case_insensitively()
    {
        var res = await Client().PostAsJsonAsync("/api/auth/register", new { username = "  Case.User-1 ", password = Password });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Case.User-1", (await ReadJsonAsync(res)).GetProperty("username").GetString());

        var dup = await Client().PostAsJsonAsync("/api/auth/register", new { username = "case.user-1", password = Password });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("Username already taken", (await ReadJsonAsync(dup)).GetProperty("message").GetString());

        // Login ignores case and surrounding whitespace.
        var (_, _, userId) = await LoginAsync(" CASE.USER-1 ");
        Assert.True(userId > 0);
    }

    [Fact]
    public async Task Login_with_unknown_user_or_wrong_password_is_401()
    {
        await RegisterAsync("login-user");

        var unknown = await Client().PostAsJsonAsync("/api/auth/login", new { username = "nobody-here", password = Password });
        var wrong = await Client().PostAsJsonAsync("/api/auth/login", new { username = "login-user", password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal("Invalid credentials", (await ReadJsonAsync(unknown)).GetProperty("message").GetString());
        Assert.Equal("Invalid credentials", (await ReadJsonAsync(wrong)).GetProperty("message").GetString());
    }

    [Fact]
    public async Task Refresh_token_cannot_be_used_as_bearer_token()
    {
        await RegisterAsync("bearer-user");
        var (access, refresh, _) = await LoginAsync("bearer-user");

        Assert.Equal(HttpStatusCode.OK, (await GetWithBearerAsync(access)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetWithBearerAsync(refresh)).StatusCode);
    }

    [Fact]
    public async Task Access_token_cannot_be_used_as_refresh_token()
    {
        await RegisterAsync("access-as-refresh");
        var (access, _, _) = await LoginAsync("access-as-refresh");

        var res = await RefreshAsync(access);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.False(string.IsNullOrEmpty((await ReadJsonAsync(res)).GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Pre_upgrade_tokens_without_token_use_are_rejected()
    {
        await RegisterAsync("legacy-user");
        var (_, _, userId) = await LoginAsync("legacy-user");

        // The claims tokens had before token_use / tv / jti existed.
        var jwt = _factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Name, "legacy-user") };
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)), SecurityAlgorithms.HmacSha256);
        var legacy = new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims, expires: DateTime.UtcNow.AddDays(1), signingCredentials: creds));

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetWithBearerAsync(legacy)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(legacy)).StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_and_rejects_reuse_of_the_old_token()
    {
        await RegisterAsync("rotate-user");
        var (_, first, _) = await LoginAsync("rotate-user");

        var res1 = await RefreshAsync(first);
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
        var second = RefreshCookie(res1);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
        var newAccess = (await ReadJsonAsync(res1)).GetProperty("accessToken").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await GetWithBearerAsync(newAccess)).StatusCode);

        // Replaying the rotated-out token fails; the new one still works (once).
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(first)).StatusCode);
        var res2 = await RefreshAsync(second!);
        Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(second!)).StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_only_that_session()
    {
        await RegisterAsync("logout-user");
        var (_, sessionA, _) = await LoginAsync("logout-user");
        var (_, sessionB, _) = await LoginAsync("logout-user");

        var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Add("Cookie", $"refresh_token={sessionA}");
        var logoutRes = await Client().SendAsync(logout);
        Assert.Equal(HttpStatusCode.OK, logoutRes.StatusCode);
        // The cookie is cleared.
        Assert.Contains(logoutRes.Headers.GetValues("Set-Cookie"), c => c.StartsWith("refresh_token=;", StringComparison.Ordinal));

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(sessionA)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(sessionB)).StatusCode);
    }

    [Fact]
    public async Task Logout_without_a_cookie_still_succeeds()
    {
        var res = await Client().PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Admin_password_reset_ends_every_session()
    {
        await RegisterAsync("reset-user");
        var (_, sessionA, userId) = await LoginAsync("reset-user");
        var (_, sessionB, _) = await LoginAsync("reset-user");

        var admin = _factory.CreateAuthenticatedClient(userId: 900_001, username: "some-admin", isAdmin: true);
        var reset = await admin.PostAsJsonAsync($"/api/admin/users/{userId}/password", new { newPassword = "brand-new-pass" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(sessionA)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(sessionB)).StatusCode);

        // The new password works and its session can refresh.
        var oldPassword = await Client().PostAsJsonAsync("/api/auth/login", new { username = "reset-user", password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        var (_, fresh, _) = await LoginAsync("reset-user", "brand-new-pass");
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(fresh)).StatusCode);
    }

    [Fact]
    public async Task Admin_password_reset_validates_length_and_protects_other_admins()
    {
        var otherAdmin = new User { Username = "other-admin", PasswordHash = "x", IsAdmin = true };
        var plain = new User { Username = "plain-target", PasswordHash = "x" };
        await _factory.WithDbAsync(async db =>
        {
            db.Users.AddRange(otherAdmin, plain);
            await db.SaveChangesAsync();
        });
        var admin = _factory.CreateAuthenticatedClient(userId: 900_002, username: "acting-admin", isAdmin: true);

        var forbidden = await admin.PostAsJsonAsync($"/api/admin/users/{otherAdmin.Id}/password", new { newPassword = "long-enough-1" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("Cannot reset another admin's password", (await ReadJsonAsync(forbidden)).GetProperty("message").GetString());

        var tooShort = await admin.PostAsJsonAsync($"/api/admin/users/{plain.Id}/password", new { newPassword = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);

        // The admin's own password is still allowed.
        var self = _factory.CreateAuthenticatedClient(userId: otherAdmin.Id, username: "other-admin", isAdmin: true);
        Assert.Equal(HttpStatusCode.NoContent, (await self.PostAsJsonAsync($"/api/admin/users/{otherAdmin.Id}/password", new { newPassword = "long-enough-1" })).StatusCode);

        await _factory.WithDbAsync(async db =>
        {
            var stored = await db.Users.AsNoTracking().SingleAsync(u => u.Id == otherAdmin.Id);
            Assert.Equal(1, stored.TokenVersion);
            Assert.True(BCrypt.Net.BCrypt.Verify("long-enough-1", stored.PasswordHash));
        });
    }

    [Fact]
    public async Task Admin_user_search_is_case_insensitive()
    {
        await _factory.WithDbAsync(async db =>
        {
            db.Users.Add(new User { Username = "MixedCaseSearch", PasswordHash = "x" });
            await db.SaveChangesAsync();
        });
        var admin = _factory.CreateAuthenticatedClient(userId: 900_003, isAdmin: true);

        var json = await ReadJsonAsync(await admin.GetAsync("/api/admin/users?search=mixedcases"));

        Assert.Equal(1, json.GetProperty("totalCount").GetInt32());
        Assert.Equal("MixedCaseSearch", json.GetProperty("items")[0].GetProperty("username").GetString());
    }

    [Fact]
    public async Task Expired_revoked_tokens_are_purged()
    {
        await _factory.WithDbAsync(async db =>
        {
            db.RevokedRefreshTokens.AddRange(
                new RevokedRefreshToken { Jti = "expired-jti", ExpiresAt = DateTime.UtcNow.AddDays(-1) },
                new RevokedRefreshToken { Jti = "live-jti", ExpiresAt = DateTime.UtcNow.AddDays(1) });
            await db.SaveChangesAsync();
        });
        var service = ActivatorUtilities.CreateInstance<ScoringDayBackgroundService>(_factory.Services);

        var purged = await service.PurgeExpiredRevokedTokensAsync();

        Assert.True(purged >= 1);
        await _factory.WithDbAsync(async db =>
        {
            Assert.False(await db.RevokedRefreshTokens.AnyAsync(t => t.Jti == "expired-jti"));
            Assert.True(await db.RevokedRefreshTokens.AnyAsync(t => t.Jti == "live-jti"));
        });
    }
}
