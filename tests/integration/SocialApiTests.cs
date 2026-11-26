using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DailyChallenges.Controllers;
using DailyChallenges.IntegrationTests.Infrastructure;
using DailyChallenges.Models;
using DailyChallenges.Services;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static DailyChallenges.IntegrationTests.Infrastructure.HttpResponseJson;

namespace DailyChallenges.IntegrationTests;

/// <summary>
/// Pins the status codes and JSON shapes of the friends, notifications, favorites, info and
/// personal-highscore endpoints. Each test gets its own factory (own database).
/// </summary>
public class SocialApiTests
{
    private const int SeededTotalXp = 120;
    private const int XpBonus = 5;
    private const string RequestNotFoundMessage = "Friend request not found.";

    /// <summary>alice and bob (plus any extra users, in order); alice has sent bob a friend request.</summary>
    private sealed record PendingRequest(int[] Ids, HttpClient Alice, HttpClient Bob, int RequestId);

    /// <summary>alice has two unread friend_request notifications, from bob and carol.</summary>
    private sealed record NotificationInbox(HttpClient Alice, HttpClient Bob, HttpClient Admin, int NotificationId);

    private static async Task<int[]> SeedUsersAsync(ApiFactory factory, params string[] names)
    {
        var users = names.Select(n => new User { Username = n, PasswordHash = "x", TotalXp = SeededTotalXp }).ToList();
        await factory.WithDbAsync(async db =>
        {
            db.Users.AddRange(users);
            await db.SaveChangesAsync();
        });
        return users.Select(u => u.Id).ToArray();
    }

    private static async Task<int> SeedGameAsync(ApiFactory factory, string name = "G")
    {
        var game = new Game { Name = name };
        await factory.WithDbAsync(async db =>
        {
            db.Games.Add(game);
            await db.SaveChangesAsync();
        });
        return game.Id;
    }

    private static async Task<PendingRequest> SeedPendingRequestAsync(ApiFactory factory, params string[] extraUsers)
    {
        var ids = await SeedUsersAsync(factory, ["alice", "bob", .. extraUsers]);
        var alice = factory.CreateAuthenticatedClient(ids[0], "alice");
        var bob = factory.CreateAuthenticatedClient(ids[1], "bob");
        return new PendingRequest(ids, alice, bob, await SendRequestAsync(alice, ids[1]));
    }

    private static async Task<PendingRequest> SeedFriendsAsync(ApiFactory factory)
    {
        var pending = await SeedPendingRequestAsync(factory);
        (await pending.Bob.PostAsync($"/api/friends/requests/{pending.RequestId}/accept", null)).EnsureSuccessStatusCode();
        return pending;
    }

    private static async Task<NotificationInbox> SeedInboxWithTwoNotificationsAsync(ApiFactory factory)
    {
        var ids = await SeedUsersAsync(factory, "alice", "bob", "carol", "admin");
        var bob = factory.CreateAuthenticatedClient(ids[1], "bob");
        await SendRequestAsync(bob, ids[0]);
        await SendRequestAsync(factory.CreateAuthenticatedClient(ids[2], "carol"), ids[0]);
        var notificationId = await factory.QueryDbAsync(db => db.Notifications.OrderBy(n => n.Id).Select(n => n.Id).FirstAsync());
        return new NotificationInbox(
            factory.CreateAuthenticatedClient(ids[0], "alice"), bob, factory.CreateAuthenticatedClient(ids[3], "admin", isAdmin: true), notificationId);
    }

    /// <summary>An admin client and alice's user id.</summary>
    private static async Task<(HttpClient Admin, int AliceId)> SeedAdminAndAliceAsync(ApiFactory factory)
    {
        var ids = await SeedUsersAsync(factory, "admin", "alice");
        return (factory.CreateAuthenticatedClient(ids[0], "admin", isAdmin: true), ids[1]);
    }

    private static async Task<string?> ErrorMessageAsync(HttpResponseMessage res)
        => (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString();

    private static string[] Keys(JsonElement element)
        => element.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    private static async Task<int> SendRequestAsync(HttpClient sender, int targetUserId)
        => (await ReadSuccessJsonAsync(await sender.PostAsJsonAsync("/api/friends/requests", new { targetUserId }))).GetProperty("id").GetInt32();

    private static async Task<int> UnreadCountAsync(HttpClient client)
        => (await ReadSuccessJsonAsync(await client.GetAsync("/api/notifications/unread-count"))).GetProperty("unreadCount").GetInt32();

    private static Task<HttpResponseMessage> AdjustXpAsync(HttpClient admin, int userId, int delta, string reason)
        => admin.PostAsJsonAsync($"/api/admin/users/{userId}/xp", new { delta, reason });

    [Fact]
    public async Task Send_request_by_username_trims_the_name_and_returns_the_request_shape()
    {
        using var factory = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(factory, "alice", "bob");
        var alice = factory.CreateAuthenticatedClient(ids[0], "alice");

        var sent = await ReadSuccessJsonAsync(await alice.PostAsJsonAsync("/api/friends/requests/by-username", new { username = "  bob " }));

        Assert.Equal(new[] { "createdAt", "id", "receiverId", "receiverUsername", "senderId", "senderUsername", "status" }, Keys(sent));
        Assert.Equal("pending", sent.GetProperty("status").GetString());
        Assert.Equal("bob", sent.GetProperty("receiverUsername").GetString());
    }

    [Fact]
    public async Task Sent_requests_list_the_pending_request()
    {
        using var factory = new FixedClockApiFactory();
        var pending = await SeedPendingRequestAsync(factory);

        var outgoing = await ReadSuccessJsonAsync(await pending.Alice.GetAsync("/api/friends/requests/sent"));

        Assert.Equal(pending.RequestId, outgoing.EnumerateArray().Single().GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Incoming_requests_show_the_sender()
    {
        using var factory = new FixedClockApiFactory();
        var pending = await SeedPendingRequestAsync(factory);

        var incoming = await ReadSuccessJsonAsync(await pending.Bob.GetAsync("/api/friends/requests/incoming"));

        Assert.Equal("alice", incoming.EnumerateArray().Single().GetProperty("senderUsername").GetString());
    }

    [Fact]
    public async Task Accepting_a_request_returns_204()
    {
        using var factory = new FixedClockApiFactory();
        var pending = await SeedPendingRequestAsync(factory);

        var res = await pending.Bob.PostAsync($"/api/friends/requests/{pending.RequestId}/accept", null);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task Friends_list_returns_the_friend_shape()
    {
        using var factory = new FixedClockApiFactory();
        var friends = await SeedFriendsAsync(factory);

        var list = await ReadSuccessJsonAsync(await friends.Alice.GetAsync("/api/friends"));

        var friend = list.EnumerateArray().Single();
        Assert.Equal(new[] { "level", "streak", "totalXp", "userId", "username" }, Keys(friend));
        Assert.Equal(friends.Ids[1], friend.GetProperty("userId").GetInt32());
        Assert.Equal(SeededTotalXp, friend.GetProperty("totalXp").GetInt32());
    }

    [Fact]
    public async Task Accepting_a_request_notifies_the_sender()
    {
        using var factory = new FixedClockApiFactory();
        var friends = await SeedFriendsAsync(factory);

        var notifications = await ReadSuccessJsonAsync(await friends.Alice.GetAsync("/api/notifications"));

        Assert.Contains(notifications.GetProperty("items").EnumerateArray(), n =>
            n.GetProperty("type").GetString() == "friend_request_accepted"
            && n.GetProperty("message").GetString() == "bob accepted your friend request.");
    }

    [Fact]
    public async Task Removing_a_friend_returns_204_and_empties_the_friends_list()
    {
        using var factory = new FixedClockApiFactory();
        var friends = await SeedFriendsAsync(factory);

        var res = await friends.Alice.DeleteAsync($"/api/friends/{friends.Ids[1]}");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Empty((await ReadSuccessJsonAsync(await friends.Alice.GetAsync("/api/friends"))).EnumerateArray());
    }

    [Fact]
    public async Task Removing_a_friend_twice_returns_404()
    {
        using var factory = new FixedClockApiFactory();
        var friends = await SeedFriendsAsync(factory);
        await friends.Alice.DeleteAsync($"/api/friends/{friends.Ids[1]}");

        var res = await friends.Alice.DeleteAsync($"/api/friends/{friends.Ids[1]}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Friend_request_to_self_returns_400_with_message()
    {
        using var factory = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(factory, "alice");

        var res = await factory.CreateAuthenticatedClient(ids[0], "alice").PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[0] });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("You cannot send a friend request to yourself.", await ErrorMessageAsync(res));
    }

    [Fact]
    public async Task Friend_request_to_unknown_username_returns_404_with_message()
    {
        using var factory = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(factory, "alice");

        var res = await factory.CreateAuthenticatedClient(ids[0], "alice").PostAsJsonAsync("/api/friends/requests/by-username", new { username = "nobody" });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("No user found with username 'nobody'.", await ErrorMessageAsync(res));
    }

    [Fact]
    public async Task Accepting_a_request_sent_to_someone_else_returns_404_with_message()
    {
        using var factory = new FixedClockApiFactory();
        var pending = await SeedPendingRequestAsync(factory, "carol");
        var carol = factory.CreateAuthenticatedClient(pending.Ids[2], "carol");

        var res = await carol.PostAsync($"/api/friends/requests/{pending.RequestId}/accept", null);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal(RequestNotFoundMessage, await ErrorMessageAsync(res));
    }

    [Fact]
    public async Task Cancelling_a_request_sent_by_someone_else_returns_404()
    {
        using var factory = new FixedClockApiFactory();
        var pending = await SeedPendingRequestAsync(factory, "carol");
        var carol = factory.CreateAuthenticatedClient(pending.Ids[2], "carol");

        var res = await carol.DeleteAsync($"/api/friends/requests/{pending.RequestId}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Sender_can_cancel_a_pending_request()
    {
        using var factory = new FixedClockApiFactory();
        var pending = await SeedPendingRequestAsync(factory);

        var res = await pending.Alice.DeleteAsync($"/api/friends/requests/{pending.RequestId}");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Equal(0, await factory.QueryDbAsync(db => db.FriendRequests.CountAsync()));
    }

    [Fact]
    public async Task Relationship_status_is_none_for_anonymous_callers()
    {
        using var factory = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(factory, "alice");

        var status = await ReadSuccessJsonAsync(await factory.CreateClient().GetAsync($"/api/friends/status/{ids[0]}"));

        Assert.Equal("none", status.GetProperty("status").GetString());
        Assert.Equal(new[] { "status" }, Keys(status));
    }

    [Fact]
    public async Task Notifications_page_zero_is_the_first_page_and_reports_totals()
    {
        using var factory = new FixedClockApiFactory();
        var inbox = await SeedInboxWithTwoNotificationsAsync(factory);

        var page = await ReadSuccessJsonAsync(await inbox.Alice.GetAsync("/api/notifications?page=0&pageSize=1"));

        Assert.Equal(new[] { "items", "totalCount", "unreadCount" }, Keys(page));
        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page.GetProperty("unreadCount").GetInt32());
        var item = page.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(new[] { "actorUserId", "createdAt", "id", "isRead", "message", "scoringDay", "type" }, Keys(item));
    }

    [Fact]
    public async Task Unread_count_returns_the_documented_shape()
    {
        using var factory = new FixedClockApiFactory();
        var inbox = await SeedInboxWithTwoNotificationsAsync(factory);

        var unread = await ReadSuccessJsonAsync(await inbox.Alice.GetAsync("/api/notifications/unread-count"));

        Assert.Equal(new[] { "unreadCount" }, Keys(unread));
        Assert.Equal(2, unread.GetProperty("unreadCount").GetInt32());
    }

    [Fact]
    public async Task Marking_a_notification_read_decrements_the_unread_count()
    {
        using var factory = new FixedClockApiFactory();
        var inbox = await SeedInboxWithTwoNotificationsAsync(factory);

        var res = await inbox.Alice.PostAsync($"/api/notifications/{inbox.NotificationId}/read", null);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Equal(1, await UnreadCountAsync(inbox.Alice));
    }

    [Fact]
    public async Task Read_all_clears_the_unread_count()
    {
        using var factory = new FixedClockApiFactory();
        var inbox = await SeedInboxWithTwoNotificationsAsync(factory);

        var res = await inbox.Alice.PostAsync("/api/notifications/read-all", null);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Equal(0, await UnreadCountAsync(inbox.Alice));
    }

    [Fact]
    public async Task Deleting_another_users_notification_returns_404_with_message()
    {
        using var factory = new FixedClockApiFactory();
        var inbox = await SeedInboxWithTwoNotificationsAsync(factory);

        var res = await inbox.Bob.DeleteAsync($"/api/notifications/{inbox.NotificationId}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("Notification not found", await ErrorMessageAsync(res));
    }

    [Fact]
    public async Task Admin_can_delete_any_notification()
    {
        using var factory = new FixedClockApiFactory();
        var inbox = await SeedInboxWithTwoNotificationsAsync(factory);

        var res = await inbox.Admin.DeleteAsync($"/api/notifications/{inbox.NotificationId}");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Equal(1, await factory.QueryDbAsync(db => db.Notifications.CountAsync()));
    }

    [Fact]
    public async Task Deleting_a_deleted_notification_returns_404()
    {
        using var factory = new FixedClockApiFactory();
        var inbox = await SeedInboxWithTwoNotificationsAsync(factory);
        (await inbox.Admin.DeleteAsync($"/api/notifications/{inbox.NotificationId}")).EnsureSuccessStatusCode();

        var res = await inbox.Alice.DeleteAsync($"/api/notifications/{inbox.NotificationId}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Adding_a_favorite_stores_it()
    {
        using var factory = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(factory, "alice");
        var gameId = await SeedGameAsync(factory);

        var res = await factory.CreateAuthenticatedClient(ids[0], "alice").PostAsync($"/api/favorites/{gameId}", null);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Equal(1, await factory.QueryDbAsync(db => db.Favorites.CountAsync()));
    }

    [Fact]
    public async Task Removing_a_favorite_is_idempotent()
    {
        using var factory = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(factory, "alice");
        var gameId = await SeedGameAsync(factory);
        var alice = factory.CreateAuthenticatedClient(ids[0], "alice");
        (await alice.PostAsync($"/api/favorites/{gameId}", null)).EnsureSuccessStatusCode();

        var first = await alice.DeleteAsync($"/api/favorites/{gameId}");
        var second = await alice.DeleteAsync($"/api/favorites/{gameId}");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(0, await factory.QueryDbAsync(db => db.Favorites.CountAsync()));
    }

    [Fact]
    public async Task Adding_a_favorite_for_an_unknown_game_returns_404_with_message()
    {
        using var factory = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(factory, "alice");
        var missingGameId = await SeedGameAsync(factory) + 100;

        var res = await factory.CreateAuthenticatedClient(ids[0], "alice").PostAsync($"/api/favorites/{missingGameId}", null);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("Game not found", await ErrorMessageAsync(res));
    }

    [Theory]
    [InlineData("GET", "/api/friends")]
    [InlineData("GET", "/api/notifications")]
    [InlineData("POST", "/api/favorites/1")]
    [InlineData("GET", "/api/leagues")]
    [InlineData("GET", "/api/games/1/personal-highscore")]
    public async Task Social_endpoints_require_a_token(string method, string url)
    {
        using var factory = new FixedClockApiFactory();

        var res = await factory.CreateClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public void Current_user_id_parameters_are_not_part_of_the_api_description()
    {
        using var factory = new FixedClockApiFactory();
        var descriptions = factory.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items.SelectMany(g => g.Items).ToList();

        var leagueDetail = descriptions.Single(d => d.HttpMethod == "GET" && d.RelativePath == "api/leagues/{id}");

        Assert.Equal(new[] { "id" }, leagueDetail.ParameterDescriptions.Select(p => p.Name));
        Assert.DoesNotContain(descriptions.SelectMany(d => d.ParameterDescriptions), p => p.Source == CurrentUserIdAttribute.Source);
    }

    [Fact]
    public async Task Personal_highscore_returns_the_callers_best_scores()
    {
        using var factory = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(factory, "alice", "bob");
        var gameId = await SeedGameAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            var day = FixedClockApiFactory.Now.Date.AddDays(-1);
            db.Submissions.AddRange(
                new Submission { GameId = gameId, UserId = ids[0], Username = "alice", Score = "10", ScoreValue = 10, ScoringDay = day },
                new Submission { GameId = gameId, UserId = ids[0], Username = "alice", Score = "30", ScoreValue = 30, ScoringDay = day.AddDays(-1) },
                new Submission { GameId = gameId, UserId = ids[1], Username = "bob", Score = "99", ScoreValue = 99, ScoringDay = day });
            await db.SaveChangesAsync();
        });

        var result = await ReadSuccessJsonAsync(await factory.CreateAuthenticatedClient(ids[0], "alice").GetAsync($"/api/games/{gameId}/personal-highscore"));

        Assert.Equal(new[] { "personalHighscore", "top" }, Keys(result));
        Assert.Equal(30, result.GetProperty("personalHighscore").GetProperty("scoreValue").GetDouble());
        Assert.Equal(new[] { 30.0, 10.0 }, result.GetProperty("top").EnumerateArray().Select(s => s.GetProperty("scoreValue").GetDouble()));
    }

    [Fact]
    public async Task Info_is_public_and_reports_the_release_info_file()
    {
        using var factory = new OfflineChangelogApiFactory();

        var info = await ReadSuccessJsonAsync(await factory.CreateClient().GetAsync("/api/info"));

        Assert.Equal(new[] { "changelog", "commit", "rawVersion", "timestamp", "version" }, Keys(info));
        Assert.Equal("dev", info.GetProperty("version").GetString());
        Assert.Equal("dev+local", info.GetProperty("rawVersion").GetString());
        Assert.Equal("local", info.GetProperty("commit").GetString());
        Assert.Equal(OfflineChangelogApiFactory.Changelog, info.GetProperty("changelog").GetString());
    }

    [Fact]
    public async Task Admin_xp_adjustment_rejects_a_zero_delta()
    {
        using var factory = new FixedClockApiFactory();
        var (admin, aliceId) = await SeedAdminAndAliceAsync(factory);

        var res = await AdjustXpAsync(admin, aliceId, delta: 0, reason: "x");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("Delta must be non-zero", await ErrorMessageAsync(res));
    }

    [Fact]
    public async Task Admin_xp_adjustment_adds_the_delta_to_total_xp()
    {
        using var factory = new FixedClockApiFactory();
        var (admin, aliceId) = await SeedAdminAndAliceAsync(factory);

        var adjusted = await ReadSuccessJsonAsync(await AdjustXpAsync(admin, aliceId, XpBonus, "bonus"));

        Assert.Equal(SeededTotalXp + XpBonus, adjusted.GetProperty("totalXp").GetInt32());
    }

    [Fact]
    public async Task Admin_xp_events_list_the_adjustment()
    {
        using var factory = new FixedClockApiFactory();
        var (admin, aliceId) = await SeedAdminAndAliceAsync(factory);
        (await AdjustXpAsync(admin, aliceId, XpBonus, "bonus")).EnsureSuccessStatusCode();

        var events = await ReadSuccessJsonAsync(await admin.GetAsync($"/api/admin/users/{aliceId}/xp-events?page=0&pageSize=500"));

        Assert.Equal(new[] { "items", "totalCount" }, Keys(events));
        Assert.Equal(1, events.GetProperty("totalCount").GetInt32());
    }
}

/// <summary>Serves a fixed changelog instead of fetching it from GitHub.</summary>
public sealed class OfflineChangelogApiFactory : ApiFactory
{
    public const string Changelog = "# Changelog";

    protected override void ConfigureServices(IServiceCollection services) =>
        services.AddHttpClient(ChangelogCache.ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new FixedResponseHandler());

    private sealed class FixedResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Changelog) });
    }
}
