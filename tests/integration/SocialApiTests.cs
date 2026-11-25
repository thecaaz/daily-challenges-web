using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DailyChallenges.Data;
using DailyChallenges.IntegrationTests.Infrastructure;
using DailyChallenges.Models;
using DailyChallenges.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DailyChallenges.IntegrationTests;

/// <summary>
/// Pins the status codes and JSON shapes of the friends, notifications, favorites, info and
/// personal-highscore endpoints. Each test gets its own factory (own database).
/// </summary>
public class SocialApiTests
{
    private static async Task<int[]> SeedUsersAsync(ApiFactory f, params string[] names)
    {
        var users = names.Select(n => new User { Username = n, PasswordHash = "x", TotalXp = 120 }).ToList();
        await f.WithDbAsync(async db =>
        {
            db.Users.AddRange(users);
            await db.SaveChangesAsync();
        });
        return users.Select(u => u.Id).ToArray();
    }

    private static async Task<int> SeedGameAsync(ApiFactory f, string name = "G")
    {
        var game = new Game { Name = name };
        await f.WithDbAsync(async db =>
        {
            db.Games.Add(game);
            await db.SaveChangesAsync();
        });
        return game.Id;
    }

    private static async Task<T> QueryAsync<T>(ApiFactory f, Func<AppDbContext, Task<T>> query)
    {
        T result = default!;
        await f.WithDbAsync(async db => result = await query(db));
        return result;
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage res)
    {
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<string?> ErrorMessageAsync(HttpResponseMessage res)
        => (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString();

    private static string[] Keys(JsonElement element)
        => element.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    private static async Task<int> SendRequestAsync(HttpClient sender, int targetUserId)
        => (await JsonAsync(await sender.PostAsJsonAsync("/api/friends/requests", new { targetUserId }))).GetProperty("id").GetInt32();

    [Fact]
    public async Task Friend_request_lifecycle_returns_the_documented_shapes()
    {
        using var f = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(f, "alice", "bob");
        var alice = f.CreateAuthenticatedClient(ids[0], "alice");
        var bob = f.CreateAuthenticatedClient(ids[1], "bob");

        var sent = await JsonAsync(await alice.PostAsJsonAsync("/api/friends/requests/by-username", new { username = "  bob " }));

        Assert.Equal(new[] { "createdAt", "id", "receiverId", "receiverUsername", "senderId", "senderUsername", "status" }, Keys(sent));
        Assert.Equal("pending", sent.GetProperty("status").GetString());
        Assert.Equal("bob", sent.GetProperty("receiverUsername").GetString());
        var outgoing = await JsonAsync(await alice.GetAsync("/api/friends/requests/sent"));
        Assert.Equal(sent.GetProperty("id").GetInt32(), outgoing.EnumerateArray().Single().GetProperty("id").GetInt32());
        var incoming = await JsonAsync(await bob.GetAsync("/api/friends/requests/incoming"));
        Assert.Equal("alice", incoming.EnumerateArray().Single().GetProperty("senderUsername").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await bob.PostAsync($"/api/friends/requests/{sent.GetProperty("id").GetInt32()}/accept", null)).StatusCode);

        var friends = await JsonAsync(await alice.GetAsync("/api/friends"));
        var friend = friends.EnumerateArray().Single();
        Assert.Equal(new[] { "level", "streak", "totalXp", "userId", "username" }, Keys(friend));
        Assert.Equal(ids[1], friend.GetProperty("userId").GetInt32());
        Assert.Equal(120, friend.GetProperty("totalXp").GetInt32());
        var accepted = await JsonAsync(await alice.GetAsync("/api/notifications"));
        Assert.Contains(accepted.GetProperty("items").EnumerateArray(), n =>
            n.GetProperty("type").GetString() == "friend_request_accepted"
            && n.GetProperty("message").GetString() == "bob accepted your friend request.");

        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync($"/api/friends/{ids[1]}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.DeleteAsync($"/api/friends/{ids[1]}")).StatusCode);
        Assert.Empty((await JsonAsync(await alice.GetAsync("/api/friends"))).EnumerateArray());
    }

    [Fact]
    public async Task Friend_request_errors_map_to_status_codes_and_messages()
    {
        using var f = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(f, "alice", "bob", "carol");
        var alice = f.CreateAuthenticatedClient(ids[0], "alice");
        var carol = f.CreateAuthenticatedClient(ids[2], "carol");

        var toSelf = await alice.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[0] });
        Assert.Equal(HttpStatusCode.BadRequest, toSelf.StatusCode);
        Assert.Equal("You cannot send a friend request to yourself.", await ErrorMessageAsync(toSelf));

        var unknown = await alice.PostAsJsonAsync("/api/friends/requests/by-username", new { username = "nobody" });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("No user found with username 'nobody'.", await ErrorMessageAsync(unknown));

        var requestId = await SendRequestAsync(alice, ids[1]);
        var notReceiver = await carol.PostAsync($"/api/friends/requests/{requestId}/accept", null);
        Assert.Equal(HttpStatusCode.NotFound, notReceiver.StatusCode);
        Assert.Equal("Friend request not found.", await ErrorMessageAsync(notReceiver));
        var notSender = await carol.DeleteAsync($"/api/friends/requests/{requestId}");
        Assert.Equal(HttpStatusCode.NotFound, notSender.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync($"/api/friends/requests/{requestId}")).StatusCode);
        Assert.Equal(0, await QueryAsync(f, db => db.FriendRequests.CountAsync()));
    }

    [Fact]
    public async Task Relationship_status_is_none_for_anonymous_callers()
    {
        using var f = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(f, "alice");

        var status = await JsonAsync(await f.CreateClient().GetAsync($"/api/friends/status/{ids[0]}"));

        Assert.Equal("none", status.GetProperty("status").GetString());
        Assert.Equal(new[] { "status" }, Keys(status));
    }

    [Fact]
    public async Task Notifications_page_read_and_delete()
    {
        using var f = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(f, "alice", "bob", "carol", "admin");
        var alice = f.CreateAuthenticatedClient(ids[0], "alice");
        var bob = f.CreateAuthenticatedClient(ids[1], "bob");
        var carol = f.CreateAuthenticatedClient(ids[2], "carol");
        var admin = f.CreateAuthenticatedClient(ids[3], "admin", isAdmin: true);
        await SendRequestAsync(bob, ids[0]);
        await SendRequestAsync(carol, ids[0]);

        var page = await JsonAsync(await alice.GetAsync("/api/notifications?page=0&pageSize=1"));

        Assert.Equal(new[] { "items", "totalCount", "unreadCount" }, Keys(page));
        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page.GetProperty("unreadCount").GetInt32());
        var first = page.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(new[] { "actorUserId", "createdAt", "id", "isRead", "message", "scoringDay", "type" }, Keys(first));
        var unread = await JsonAsync(await alice.GetAsync("/api/notifications/unread-count"));
        Assert.Equal(new[] { "unreadCount" }, Keys(unread));
        Assert.Equal(2, unread.GetProperty("unreadCount").GetInt32());

        var firstId = first.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsync($"/api/notifications/{firstId}/read", null)).StatusCode);
        Assert.Equal(1, (await JsonAsync(await alice.GetAsync("/api/notifications/unread-count"))).GetProperty("unreadCount").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsync("/api/notifications/read-all", null)).StatusCode);
        Assert.Equal(0, (await JsonAsync(await alice.GetAsync("/api/notifications/unread-count"))).GetProperty("unreadCount").GetInt32());

        var foreign = await bob.DeleteAsync($"/api/notifications/{firstId}");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal("Notification not found", await ErrorMessageAsync(foreign));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/notifications/{firstId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.DeleteAsync($"/api/notifications/{firstId}")).StatusCode);
        Assert.Equal(1, await QueryAsync(f, db => db.Notifications.CountAsync()));
    }

    [Fact]
    public async Task Favorites_add_and_remove()
    {
        using var f = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(f, "alice");
        var gameId = await SeedGameAsync(f);
        var alice = f.CreateAuthenticatedClient(ids[0], "alice");

        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsync($"/api/favorites/{gameId}", null)).StatusCode);
        Assert.Equal(1, await QueryAsync(f, db => db.Favorites.CountAsync()));
        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync($"/api/favorites/{gameId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync($"/api/favorites/{gameId}")).StatusCode);
        Assert.Equal(0, await QueryAsync(f, db => db.Favorites.CountAsync()));

        var missing = await alice.PostAsync($"/api/favorites/{gameId + 100}", null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("Game not found", await ErrorMessageAsync(missing));
    }

    [Fact]
    public async Task Social_endpoints_require_a_token()
    {
        using var f = new FixedClockApiFactory();
        var anonymous = f.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/friends")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/favorites/1", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/leagues")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/games/1/personal-highscore")).StatusCode);
    }

    [Fact]
    public async Task Personal_highscore_returns_the_callers_best_scores()
    {
        using var f = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(f, "alice", "bob");
        var gameId = await SeedGameAsync(f);
        await f.WithDbAsync(async db =>
        {
            var day = FixedClockApiFactory.Now.Date.AddDays(-1);
            db.Submissions.AddRange(
                new Submission { GameId = gameId, UserId = ids[0], Username = "alice", Score = "10", ScoreValue = 10, ScoringDay = day },
                new Submission { GameId = gameId, UserId = ids[0], Username = "alice", Score = "30", ScoreValue = 30, ScoringDay = day.AddDays(-1) },
                new Submission { GameId = gameId, UserId = ids[1], Username = "bob", Score = "99", ScoreValue = 99, ScoringDay = day });
            await db.SaveChangesAsync();
        });

        var result = await JsonAsync(await f.CreateAuthenticatedClient(ids[0], "alice").GetAsync($"/api/games/{gameId}/personal-highscore"));

        Assert.Equal(new[] { "personalHighscore", "top" }, Keys(result));
        Assert.Equal(30, result.GetProperty("personalHighscore").GetProperty("scoreValue").GetDouble());
        Assert.Equal(new[] { 30.0, 10.0 }, result.GetProperty("top").EnumerateArray().Select(s => s.GetProperty("scoreValue").GetDouble()));
    }

    [Fact]
    public async Task Info_is_public_and_reports_the_release_info_file()
    {
        using var f = new OfflineChangelogApiFactory();

        var info = await JsonAsync(await f.CreateClient().GetAsync("/api/info"));

        Assert.Equal(new[] { "changelog", "commit", "rawVersion", "timestamp", "version" }, Keys(info));
        Assert.Equal("dev", info.GetProperty("version").GetString());
        Assert.Equal("dev+local", info.GetProperty("rawVersion").GetString());
        Assert.Equal("local", info.GetProperty("commit").GetString());
        Assert.Equal(OfflineChangelogApiFactory.Changelog, info.GetProperty("changelog").GetString());
    }

    [Fact]
    public async Task Admin_xp_adjustment_rejects_a_zero_delta()
    {
        using var f = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(f, "admin", "alice");
        var admin = f.CreateAuthenticatedClient(ids[0], "admin", isAdmin: true);

        var zero = await admin.PostAsJsonAsync($"/api/admin/users/{ids[1]}/xp", new { delta = 0, reason = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        Assert.Equal("Delta must be non-zero", await ErrorMessageAsync(zero));

        var adjusted = await JsonAsync(await admin.PostAsJsonAsync($"/api/admin/users/{ids[1]}/xp", new { delta = 5, reason = "bonus" }));
        Assert.Equal(125, adjusted.GetProperty("totalXp").GetInt32());

        var events = await JsonAsync(await admin.GetAsync($"/api/admin/users/{ids[1]}/xp-events?page=0&pageSize=500"));
        Assert.Equal(new[] { "items", "totalCount" }, Keys(events));
        Assert.Equal(1, events.GetProperty("totalCount").GetInt32());
    }
}

/// <summary>Serves a fixed changelog instead of fetching it from GitHub.</summary>
public sealed class OfflineChangelogApiFactory : ApiFactory
{
    public const string Changelog = "# Changelog";

    protected override void ConfigureServices(IServiceCollection services) =>
        services.AddHttpClient(InfoService.ChangelogClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new FixedResponseHandler());

    private sealed class FixedResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Changelog) });
    }
}
