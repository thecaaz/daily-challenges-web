using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DailyChallenges.Data;
using DailyChallenges.IntegrationTests.Infrastructure;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.IntegrationTests;

/// <summary>
/// Leagues, friends, favorites and notifications against the real API, DbContext and migrations (H8, B7, B8).
/// Each test gets its own factory (own database). Clock: <see cref="FixedClockApiFactory.Now"/>.
/// </summary>
public class LeaguesAndSocialTests
{
    private static async Task<int[]> SeedUsersAsync(ApiFactory f, params string[] names)
    {
        var users = names.Select(n => new User { Username = n, PasswordHash = "x" }).ToList();
        await f.WithDbAsync(async db =>
        {
            db.Users.AddRange(users);
            await db.SaveChangesAsync();
        });
        return users.Select(u => u.Id).ToArray();
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

    private static async Task<int> CreateLeagueAsync(HttpClient owner, string name = "League")
        => (await JsonAsync(await owner.PostAsJsonAsync("/api/leagues", new { name }))).GetProperty("id").GetInt32();

    private static async Task<int> InviteAsync(HttpClient owner, int leagueId, string username)
        => (await JsonAsync(await owner.PostAsJsonAsync($"/api/leagues/{leagueId}/invitations", new { username }))).GetProperty("id").GetInt32();

    // ── H8: rename / delete ───────────────────────────────────────────────────

    [Fact]
    public async Task Owner_can_rename_and_delete_a_league_with_members_and_invitations()
    {
        using var f = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(f, "owner", "member", "invitee");
        var owner = f.CreateAuthenticatedClient(ids[0], "owner");
        var member = f.CreateAuthenticatedClient(ids[1], "member");

        var leagueId = await CreateLeagueAsync(owner, "Before");
        var inviteId = await InviteAsync(owner, leagueId, "member");
        await JsonAsync(await member.PostAsync($"/api/leagues/invitations/{inviteId}/accept", null));
        await InviteAsync(owner, leagueId, "invitee");
        await JsonAsync(await owner.PostAsync($"/api/leagues/{leagueId}/invite-link", null));

        var renamed = await JsonAsync(await owner.PatchAsJsonAsync($"/api/leagues/{leagueId}/name", new { name = "After" }));
        Assert.Equal("After", renamed.GetProperty("name").GetString());
        Assert.Equal(2, renamed.GetProperty("memberCount").GetInt32());
        Assert.Equal("After", await QueryAsync(f, db => db.Leagues.Where(l => l.Id == leagueId).Select(l => l.Name).SingleAsync()));
        // The owner's user row was not written back from the read-only graph.
        Assert.Equal("owner", await QueryAsync(f, db => db.Users.Where(u => u.Id == ids[0]).Select(u => u.Username).SingleAsync()));

        // A member who is not the owner can do neither.
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PatchAsJsonAsync($"/api/leagues/{leagueId}/name", new { name = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.DeleteAsync($"/api/leagues/{leagueId}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/leagues/{leagueId}")).StatusCode);
        Assert.False(await QueryAsync(f, db => db.Leagues.AnyAsync()));
        Assert.False(await QueryAsync(f, db => db.LeagueMembers.AnyAsync()));
        Assert.False(await QueryAsync(f, db => db.LeagueInvitations.AnyAsync()));
        Assert.Equal(HttpStatusCode.NotFound, (await owner.DeleteAsync($"/api/leagues/{leagueId}")).StatusCode);
    }

    // ── Invitations ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Duplicate_pending_invite_is_409_and_the_database_rejects_one_too()
    {
        using var f = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(f, "owner", "bob");
        var owner = f.CreateAuthenticatedClient(ids[0], "owner");
        var leagueId = await CreateLeagueAsync(owner);

        await InviteAsync(owner, leagueId, "bob");
        var again = await owner.PostAsJsonAsync($"/api/leagues/{leagueId}/invitations", new { username = "bob" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        // The filtered unique index backs the service check (concurrent invites).
        await f.WithDbAsync(async db =>
        {
            db.LeagueInvitations.Add(new LeagueInvitation { LeagueId = leagueId, InviterId = ids[0], InviteeId = ids[1], Status = LeagueInvitationStatus.Pending });
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.True(ex.IsUniqueViolation());
        });
        Assert.Equal(1, await QueryAsync(f, db => db.LeagueInvitations.CountAsync()));
    }

    [Fact]
    public async Task Reinvite_after_a_decline_is_409_for_24_hours()
    {
        using var f = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(f, "owner", "bob");
        var owner = f.CreateAuthenticatedClient(ids[0], "owner");
        var bob = f.CreateAuthenticatedClient(ids[1], "bob");
        var leagueId = await CreateLeagueAsync(owner);

        var inviteId = await InviteAsync(owner, leagueId, "bob");
        Assert.Equal(HttpStatusCode.NoContent, (await bob.PostAsync($"/api/leagues/invitations/{inviteId}/decline", null)).StatusCode);
        // Declining twice: no longer pending.
        Assert.Equal(HttpStatusCode.Conflict, (await bob.PostAsync($"/api/leagues/invitations/{inviteId}/decline", null)).StatusCode);

        f.Clock.Advance(TimeSpan.FromHours(23));
        var tooSoon = await owner.PostAsJsonAsync($"/api/leagues/{leagueId}/invitations", new { username = "bob" });
        Assert.Equal(HttpStatusCode.Conflict, tooSoon.StatusCode);

        f.Clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1));
        await InviteAsync(owner, leagueId, "bob");
    }

    [Fact]
    public async Task Accepting_twice_or_after_cancel_is_409_and_links_are_single_use()
    {
        using var f = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(f, "owner", "bob", "carol", "dave");
        var owner = f.CreateAuthenticatedClient(ids[0], "owner");
        var bob = f.CreateAuthenticatedClient(ids[1], "bob");
        var carol = f.CreateAuthenticatedClient(ids[2], "carol");
        var dave = f.CreateAuthenticatedClient(ids[3], "dave");
        var leagueId = await CreateLeagueAsync(owner);

        var inviteId = await InviteAsync(owner, leagueId, "bob");
        await JsonAsync(await bob.PostAsync($"/api/leagues/invitations/{inviteId}/accept", null));
        Assert.Equal(HttpStatusCode.Conflict, (await bob.PostAsync($"/api/leagues/invitations/{inviteId}/accept", null)).StatusCode);

        var cancelled = await InviteAsync(owner, leagueId, "carol");
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/leagues/invitations/{cancelled}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await carol.PostAsync($"/api/leagues/invitations/{cancelled}/accept", null)).StatusCode);

        // A member using a link does not use it up; the first non-member does.
        var token = (await JsonAsync(await owner.PostAsync($"/api/leagues/{leagueId}/invite-link", null))).GetProperty("token").GetString();
        Assert.Equal(HttpStatusCode.Conflict, (await bob.PostAsync($"/api/leagues/join/{token}", null)).StatusCode);
        await JsonAsync(await carol.PostAsync($"/api/leagues/join/{token}", null));
        Assert.Equal(HttpStatusCode.NotFound, (await dave.PostAsync($"/api/leagues/join/{token}", null)).StatusCode);

        Assert.Equal(3, await QueryAsync(f, db => db.LeagueMembers.CountAsync(m => m.LeagueId == leagueId)));
        var notifications = await QueryAsync(f, db => db.Notifications.Where(n => n.Type == "league_joined").Select(n => n.ActorUserId).ToListAsync());
        Assert.Equal(new int?[] { ids[1], ids[2] }, notifications.OrderBy(x => x));
    }

    // ── Game summaries ────────────────────────────────────────────────────────

    [Fact]
    public async Task Game_summaries_return_recentPlayDays_parallel_to_recentPlays()
    {
        using var f = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(f, "owner");
        var owner = f.CreateAuthenticatedClient(ids[0], "owner");
        var leagueId = await CreateLeagueAsync(owner);
        var today = FixedClockApiFactory.Now.Date; // reset 00:00 → the game's current day is the UTC date
        await f.WithDbAsync(async db =>
        {
            var game = new Game { Name = "G", ResetTime = TimeSpan.Zero };
            db.Games.Add(game);
            await db.SaveChangesAsync();
            foreach (var day in new[] { today.AddDays(-2), today })
            {
                db.Submissions.Add(new Submission
                {
                    GameId = game.Id, UserId = ids[0], Username = "owner", Score = "5", ScoreValue = 5,
                    ScoringDay = day, CreatedAt = day.AddHours(1), ScreenshotData = new byte[] { 1 }, ScreenshotContentType = "image/png",
                });
            }
            await db.SaveChangesAsync();
        });

        var body = await JsonAsync(await owner.GetAsync($"/api/leagues/{leagueId}/game-summaries?days=3"));
        var item = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal(new[] { 1, 0, 1 }, item.GetProperty("recentPlays").EnumerateArray().Select(e => e.GetInt32()));
        Assert.Equal(
            new[] { today.AddDays(-2), today.AddDays(-1), today }.Select(d => d.ToString("yyyy-MM-dd")),
            item.GetProperty("recentPlayDays").EnumerateArray().Select(e => e.GetString()));
    }

    // ── Friends ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reverse_direction_friend_request_is_409_and_creates_no_second_row()
    {
        using var f = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(f, "alice", "bob");
        var alice = f.CreateAuthenticatedClient(ids[0], "alice");
        var bob = f.CreateAuthenticatedClient(ids[1], "bob");

        await JsonAsync(await alice.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[1] }));
        Assert.Equal(HttpStatusCode.Conflict, (await bob.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[0] })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await alice.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[1] })).StatusCode);
        Assert.Equal(1, await QueryAsync(f, db => db.FriendRequests.CountAsync()));
    }

    [Fact]
    public async Task Declined_request_reads_as_none_and_can_be_resent_after_24_hours()
    {
        using var f = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(f, "alice", "bob");
        var alice = f.CreateAuthenticatedClient(ids[0], "alice");
        var bob = f.CreateAuthenticatedClient(ids[1], "bob");

        var requestId = (await JsonAsync(await alice.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[1] }))).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await bob.PostAsync($"/api/friends/requests/{requestId}/reject", null)).StatusCode);

        Assert.Equal("none", await StatusAsync(alice, ids[1]));
        Assert.Equal("none", await StatusAsync(bob, ids[0]));
        Assert.Empty((await JsonAsync(await bob.GetAsync("/api/friends/requests/incoming"))).EnumerateArray());

        f.Clock.Advance(TimeSpan.FromHours(23));
        Assert.Equal(HttpStatusCode.Conflict, (await alice.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[1] })).StatusCode);

        f.Clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1));
        var resent = await JsonAsync(await alice.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[1] }));
        Assert.Equal(requestId, resent.GetProperty("id").GetInt32()); // the same row, turned back into Pending
        Assert.Equal("pending_sent", await StatusAsync(alice, ids[1]));
        Assert.Equal("pending_received", await StatusAsync(bob, ids[0]));
        Assert.Equal(1, await QueryAsync(f, db => db.FriendRequests.CountAsync()));
    }

    [Fact]
    public async Task The_user_who_declined_can_send_a_request_right_away()
    {
        using var f = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(f, "alice", "bob");
        var alice = f.CreateAuthenticatedClient(ids[0], "alice");
        var bob = f.CreateAuthenticatedClient(ids[1], "bob");

        var requestId = (await JsonAsync(await alice.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[1] }))).GetProperty("id").GetInt32();
        await bob.PostAsync($"/api/friends/requests/{requestId}/reject", null);

        await JsonAsync(await bob.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[0] }));
        Assert.Equal("pending_received", await StatusAsync(alice, ids[1]));
        await alice.PostAsync($"/api/friends/requests/{requestId}/accept", null);
        Assert.Equal("friends", await StatusAsync(alice, ids[1]));
    }

    private static async Task<string?> StatusAsync(HttpClient client, int targetUserId)
        => (await JsonAsync(await client.GetAsync($"/api/friends/status/{targetUserId}"))).GetProperty("status").GetString();

    // ── Notifications ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Friend_request_notification_cooldown_matches_the_exact_sender()
    {
        using var f = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(f, "bobby", "bob", "carol");
        var bobby = f.CreateAuthenticatedClient(ids[0], "bobby");
        var bob = f.CreateAuthenticatedClient(ids[1], "bob");
        var carol = ids[2];

        await JsonAsync(await bobby.PostAsJsonAsync("/api/friends/requests", new { targetUserId = carol }));
        // "bobby sent you a friend request." contains "bob": the old substring check skipped bob's notification.
        var requestId = (await JsonAsync(await bob.PostAsJsonAsync("/api/friends/requests", new { targetUserId = carol }))).GetProperty("id").GetInt32();

        // bob cancels and asks again within the cooldown: still only one notification from bob.
        Assert.Equal(HttpStatusCode.NoContent, (await bob.DeleteAsync($"/api/friends/requests/{requestId}")).StatusCode);
        await JsonAsync(await bob.PostAsJsonAsync("/api/friends/requests", new { targetUserId = carol }));

        var page = await JsonAsync(await f.CreateAuthenticatedClient(carol, "carol").GetAsync("/api/notifications"));
        var actors = page.GetProperty("items").EnumerateArray()
            .Where(n => n.GetProperty("type").GetString() == "friend_request")
            .Select(n => n.GetProperty("actorUserId").GetInt32())
            .OrderBy(x => x);
        Assert.Equal(new[] { ids[0], ids[1] }.OrderBy(x => x), actors);
    }

    // ── Favorites ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Favoriting_twice_is_a_no_op()
    {
        using var f = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(f, "alice");
        var gameId = 0;
        await f.WithDbAsync(async db =>
        {
            var game = new Game { Name = "G" };
            db.Games.Add(game);
            await db.SaveChangesAsync();
            gameId = game.Id;
        });
        var alice = f.CreateAuthenticatedClient(ids[0], "alice");

        var first = await alice.PostAsync($"/api/favorites/{gameId}", null);
        var second = await alice.PostAsync($"/api/favorites/{gameId}", null);
        Assert.True(first.IsSuccessStatusCode);
        Assert.Equal(first.StatusCode, second.StatusCode);
        Assert.Equal(1, await QueryAsync(f, db => db.Favorites.CountAsync()));
    }
}

/// <summary>Real concurrency: a file database, one connection per request (see <see cref="FileDbApiFactory"/>).</summary>
public class LeaguesAndSocialConcurrencyTests : IClassFixture<FileDbApiFactory>
{
    private readonly FileDbApiFactory _f;

    public LeaguesAndSocialConcurrencyTests(FileDbApiFactory f) => _f = f;

    private async Task<int[]> SeedUsersAsync(params string[] names)
    {
        var users = names.Select(n => new User { Username = $"{n}-{Guid.NewGuid():N}", PasswordHash = "x" }).ToList();
        await _f.WithDbAsync(async db =>
        {
            db.Users.AddRange(users);
            await db.SaveChangesAsync();
        });
        return users.Select(u => u.Id).ToArray();
    }

    private async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        T result = default!;
        await _f.WithDbAsync(async db => result = await query(db));
        return result;
    }

    [Fact]
    public async Task Concurrent_double_accept_lets_exactly_one_through()
    {
        for (var round = 0; round < 5; round++)
        {
            var ids = await SeedUsersAsync("owner", "joiner");
            int leagueId = 0, invitationId = 0;
            await _f.WithDbAsync(async db =>
            {
                var league = new League { Name = "L", OwnerId = ids[0], Members = { new LeagueMember { UserId = ids[0], Role = LeagueRole.Owner } } };
                db.Leagues.Add(league);
                await db.SaveChangesAsync();
                var invitation = new LeagueInvitation { LeagueId = league.Id, InviterId = ids[0], InviteeId = ids[1], Status = LeagueInvitationStatus.Pending };
                db.LeagueInvitations.Add(invitation);
                await db.SaveChangesAsync();
                (leagueId, invitationId) = (league.Id, invitation.Id);
            });

            var joiner = _f.CreateAuthenticatedClient(ids[1], "joiner");
            var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
                joiner.PostAsync($"/api/leagues/invitations/{invitationId}/accept", null)));

            var codes = responses.Select(r => r.StatusCode).OrderBy(c => c).ToArray();
            Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, codes);
            Assert.Equal(2, await QueryAsync(db => db.LeagueMembers.CountAsync(m => m.LeagueId == leagueId)));
            Assert.Equal(1, await QueryAsync(db => db.Notifications.CountAsync(n => n.UserId == ids[0] && n.Type == "league_joined")));
        }
    }

    [Fact]
    public async Task Concurrent_friend_requests_in_both_directions_create_one_row()
    {
        var ids = await SeedUsersAsync("a", "b");
        var a = _f.CreateAuthenticatedClient(ids[0], "a");
        var b = _f.CreateAuthenticatedClient(ids[1], "b");

        var responses = await Task.WhenAll(
            a.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[1] }),
            b.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[0] }),
            a.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[1] }));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(1, await QueryAsync(db => db.FriendRequests.CountAsync(fr =>
            (fr.SenderId == ids[0] && fr.ReceiverId == ids[1]) || (fr.SenderId == ids[1] && fr.ReceiverId == ids[0]))));
    }

    [Fact]
    public async Task Concurrent_double_favorite_is_a_no_op()
    {
        var ids = await SeedUsersAsync("fav");
        var gameId = 0;
        await _f.WithDbAsync(async db =>
        {
            var game = new Game { Name = "G" };
            db.Games.Add(game);
            await db.SaveChangesAsync();
            gameId = game.Id;
        });
        var client = _f.CreateAuthenticatedClient(ids[0], "fav");

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostAsync($"/api/favorites/{gameId}", null)));

        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode, r.StatusCode.ToString()));
        Assert.Equal(1, await QueryAsync(db => db.Favorites.CountAsync(x => x.UserId == ids[0] && x.GameId == gameId)));
    }
}
