using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DailyChallenges.IntegrationTests.Infrastructure;
using DailyChallenges.Models;
using DailyChallenges.Services;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.IntegrationTests;

/// <summary>
/// Replaying a revoked refresh token: shortly after rotation it is just rejected (concurrent tabs, lost
/// responses); long after rotation it counts as theft and ends every session; after logout it is just rejected.
/// Each test owns its database and a fixed clock (2026-06-15 10:00 UTC).
/// </summary>
public class AuthRefreshReuseTests
{
    private const string Password = "correct-horse-1";
    private static readonly TimeSpan JustInside = RefreshTokenRevocations.ReuseGracePeriod - TimeSpan.FromSeconds(1);
    private static readonly TimeSpan JustOutside = RefreshTokenRevocations.ReuseGracePeriod + TimeSpan.FromSeconds(1);

    private static async Task<(string Refresh, int UserId)> RegisterAndLoginAsync(FixedClockApiFactory factory, string username)
    {
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateCookielessClient().PostAsJsonAsync("/api/auth/register", new { username, password = Password })).StatusCode);
        var res = await factory.CreateCookielessClient().PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return (RefreshCookie.ValueFrom(res)!, doc.RootElement.GetProperty("user").GetProperty("id").GetInt32());
    }

    private static Task<HttpResponseMessage> RefreshAsync(FixedClockApiFactory factory, string refreshToken)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        req.Headers.Add("Cookie", $"refresh_token={refreshToken}");
        return factory.CreateCookielessClient().SendAsync(req);
    }

    private static async Task<string> RotateAsync(FixedClockApiFactory factory, string refreshToken)
    {
        var res = await RefreshAsync(factory, refreshToken);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return RefreshCookie.ValueFrom(res)!;
    }

    private static async Task<int> TokenVersionAsync(FixedClockApiFactory factory, int userId)
    {
        var version = 0;
        await factory.WithDbAsync(async db => version = await db.Users.Where(u => u.Id == userId).Select(u => u.TokenVersion).SingleAsync());
        return version;
    }

    [Fact]
    public async Task Replay_within_the_grace_period_is_rejected_but_keeps_the_other_sessions()
    {
        using var factory = new FixedClockApiFactory();
        var (first, userId) = await RegisterAndLoginAsync(factory, "grace-user");
        var second = await RotateAsync(factory, first);

        factory.Clock.Advance(JustInside);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(factory, first)).StatusCode);

        Assert.Equal(0, await TokenVersionAsync(factory, userId));
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(factory, second)).StatusCode);
    }

    private static async Task<string> LoginAgainAsync(FixedClockApiFactory factory, string username)
    {
        var res = await factory.CreateCookielessClient().PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return RefreshCookie.ValueFrom(res)!;
    }

    [Fact]
    public async Task Replay_after_the_grace_period_is_treated_as_theft_and_ends_every_session()
    {
        using var factory = new FixedClockApiFactory();
        var (first, userId) = await RegisterAndLoginAsync(factory, "theft-user");
        var second = await RotateAsync(factory, first);
        var otherDevice = await LoginAgainAsync(factory, "theft-user");

        factory.Clock.Advance(JustOutside);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(factory, first)).StatusCode);

        Assert.Equal(1, await TokenVersionAsync(factory, userId));
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(factory, second)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(factory, otherDevice)).StatusCode);
    }

    [Fact]
    public async Task Logging_in_again_after_a_theft_response_works()
    {
        using var factory = new FixedClockApiFactory();
        var (first, _) = await RegisterAndLoginAsync(factory, "theft-relogin-user");
        await RotateAsync(factory, first);
        factory.Clock.Advance(JustOutside);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(factory, first)).StatusCode);

        var fresh = await LoginAgainAsync(factory, "theft-relogin-user");

        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(factory, fresh)).StatusCode);
    }

    [Fact]
    public async Task Replay_of_a_logged_out_token_is_rejected_without_ending_other_sessions()
    {
        using var factory = new FixedClockApiFactory();
        var (sessionA, userId) = await RegisterAndLoginAsync(factory, "logout-replay-user");
        var sessionBRes = await factory.CreateCookielessClient().PostAsJsonAsync("/api/auth/login", new { username = "logout-replay-user", password = Password });
        var sessionB = RefreshCookie.ValueFrom(sessionBRes)!;

        var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Add("Cookie", $"refresh_token={sessionA}");
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateCookielessClient().SendAsync(logout)).StatusCode);
        var row = await QueryRevokedAsync(factory);
        Assert.Equal(RevokedRefreshTokenReason.Logout, row.Reason);
        Assert.Equal(FixedClockApiFactory.Now, row.RevokedAt);

        factory.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(factory, sessionA)).StatusCode);

        Assert.Equal(0, await TokenVersionAsync(factory, userId));
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(factory, sessionB)).StatusCode);
    }

    private static async Task<RevokedRefreshToken> QueryRevokedAsync(FixedClockApiFactory factory)
    {
        RevokedRefreshToken row = null!;
        await factory.WithDbAsync(async db => row = await db.RevokedRefreshTokens.AsNoTracking().SingleAsync());
        return row;
    }
}
