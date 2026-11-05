using System.Net;
using System.Text.Json;
using DailyChallenges.IntegrationTests.Infrastructure;
using DailyChallenges.Models;

namespace DailyChallenges.IntegrationTests;

/// <summary>End-to-end checks through the real HTTP pipeline (routing, auth, middleware, JSON options).</summary>
public class ApiSmokeTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ApiSmokeTests(ApiFactory factory) => _factory = factory;

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private async Task<int> SeedUserAsync(string username)
    {
        var user = new User { Username = username, PasswordHash = "x" };
        await _factory.WithDbAsync(async db =>
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        });
        return user.Id;
    }

    [Fact]
    public async Task Get_games_is_public()
    {
        var res = await _factory.CreateClient().GetAsync("/api/games");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(JsonValueKind.Array, (await ReadJsonAsync(res)).ValueKind);
    }

    [Fact]
    public async Task Authorized_endpoint_requires_a_valid_token()
    {
        var anonymous = await _factory.CreateClient().GetAsync("/api/notifications/unread-count");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var badToken = _factory.CreateClient();
        badToken.DefaultRequestHeaders.Authorization = new("Bearer", "not-a-jwt");
        Assert.Equal(HttpStatusCode.Unauthorized, (await badToken.GetAsync("/api/notifications/unread-count")).StatusCode);

        var authed = await _factory.CreateAuthenticatedClient(userId: 4242).GetAsync("/api/notifications/unread-count");
        Assert.Equal(HttpStatusCode.OK, authed.StatusCode);
        Assert.Equal(0, (await ReadJsonAsync(authed)).GetProperty("unreadCount").GetInt32());
    }

    [Fact]
    public async Task Admin_endpoint_requires_admin_role()
    {
        var user = await _factory.CreateAuthenticatedClient(userId: 1, isAdmin: false).GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, user.StatusCode);

        var admin = await _factory.CreateAuthenticatedClient(userId: 1, isAdmin: true).GetAsync("/api/admin/users?pageSize=100000");
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
    }

    [Fact]
    public async Task Domain_exception_becomes_json_error_with_status()
    {
        var ownerId = await SeedUserAsync("notif-owner");
        var otherId = await SeedUserAsync("notif-other");
        var notification = new Notification { UserId = ownerId, Message = "hi", Type = "achievement", ScoringDay = new DateTime(2026, 4, 1) };
        await _factory.WithDbAsync(async db =>
        {
            db.Notifications.Add(notification);
            await db.SaveChangesAsync();
        });

        // Someone else's notification reads as 404, same as a missing one.
        var res = await _factory.CreateAuthenticatedClient(otherId).DeleteAsync($"/api/notifications/{notification.Id}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("Notification not found", (await ReadJsonAsync(res)).GetProperty("message").GetString());
    }

    [Fact]
    public async Task DateTimes_are_serialized_as_utc_with_z()
    {
        var userId = await SeedUserAsync("dates-user");
        await _factory.WithDbAsync(async db =>
        {
            db.Notifications.Add(new Notification
            {
                UserId = userId, Message = "m", Type = "achievement",
                ScoringDay = new DateTime(2026, 4, 1),
                CreatedAt = new DateTime(2026, 4, 1, 10, 30, 0, DateTimeKind.Utc)
            });
            await db.SaveChangesAsync();
        });

        var res = await _factory.CreateAuthenticatedClient(userId).GetAsync("/api/notifications");
        var item = (await ReadJsonAsync(res)).GetProperty("items")[0];

        // SQLite returns Kind=Unspecified; the converter must label it UTC without shifting it.
        Assert.Equal("2026-04-01T10:30:00Z", item.GetProperty("createdAt").GetString());
        Assert.Equal("2026-04-01", item.GetProperty("scoringDay").GetString());
    }

    [Fact]
    public async Task Game_list_signals_images_by_content_type_and_image_endpoint_serves_blob()
    {
        var withImage = new Game { Name = "With image", ScreenshotData = new byte[] { 0x89, 0x50, 0x4E, 0x47 }, ScreenshotContentType = "image/png" };
        var withoutImage = new Game { Name = "Without image" };
        await _factory.WithDbAsync(async db =>
        {
            db.Games.AddRange(withImage, withoutImage);
            await db.SaveChangesAsync();
        });

        var client = _factory.CreateClient();
        var games = (await ReadJsonAsync(await client.GetAsync("/api/games"))).EnumerateArray().ToList();

        var a = games.Single(g => g.GetProperty("id").GetInt32() == withImage.Id);
        var b = games.Single(g => g.GetProperty("id").GetInt32() == withoutImage.Id);
        Assert.Equal($"/api/games/{withImage.Id}/image", a.GetProperty("imageUrl").GetString());
        Assert.False(b.TryGetProperty("imageUrl", out _));

        var image = await client.GetAsync($"/api/games/{withImage.Id}/image");
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        Assert.Equal(withImage.ScreenshotData, await image.Content.ReadAsByteArrayAsync());

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/games/{withoutImage.Id}/image")).StatusCode);
    }
}
