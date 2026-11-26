using System.Net;
using System.Net.Http.Json;
using DailyChallenges.Data;
using DailyChallenges.IntegrationTests.Infrastructure;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
using static DailyChallenges.IntegrationTests.Infrastructure.HttpResponseJson;

namespace DailyChallenges.IntegrationTests;

/// <summary>
/// Leagues, friends, favorites and notifications against the real API, DbContext and migrations (H8, B7, B8).
/// Each test gets its own factory (own database). Clock: <see cref="FixedClockApiFactory.Now"/>.
/// </summary>
public class LeaguesAndSocialTests
{
    private static async Task<int[]> SeedUsersAsync(ApiFactory factory, params string[] names)
    {
        var users = names.Select(n => new User { Username = n, PasswordHash = "x" }).ToList();
        await factory.WithDbAsync(async db =>
        {
            db.Users.AddRange(users);
            await db.SaveChangesAsync();
        });
        return users.Select(u => u.Id).ToArray();
    }

    private static async Task<int> CreateLeagueAsync(HttpClient owner, string name = "League")
        => (await ReadSuccessJsonAsync(await owner.PostAsJsonAsync("/api/leagues", new { name }))).GetProperty("id").GetInt32();

    private static async Task<int> InviteAsync(HttpClient owner, int leagueId, string username)
        => (await ReadSuccessJsonAsync(await owner.PostAsJsonAsync($"/api/leagues/{leagueId}/invitations", new { username }))).GetProperty("id").GetInt32();

    // ── H8: rename / delete ───────────────────────────────────────────────────

    private sealed record LeagueSeed(int[] Ids, HttpClient Owner, HttpClient Member, int LeagueId);

    /// <summary>A league "Before" with a member, a pending invitation for "invitee" and an invite link.</summary>
    private static async Task<LeagueSeed> SeedLeagueWithMemberInviteAndLinkAsync(ApiFactory factory)
    {
        var ids = await SeedUsersAsync(factory, "owner", "member", "invitee");
        var owner = factory.CreateAuthenticatedClient(ids[0], "owner");
        var member = factory.CreateAuthenticatedClient(ids[1], "member");

        var leagueId = await CreateLeagueAsync(owner, "Before");
        var inviteId = await InviteAsync(owner, leagueId, "member");
        await ReadSuccessJsonAsync(await member.PostAsync($"/api/leagues/invitations/{inviteId}/accept", null));
        await InviteAsync(owner, leagueId, "invitee");
        await ReadSuccessJsonAsync(await owner.PostAsync($"/api/leagues/{leagueId}/invite-link", null));
        return new LeagueSeed(ids, owner, member, leagueId);
    }

    /// <summary>The owner's user row must not be written back from the read-only graph.</summary>
    [Fact]
    public async Task Owner_can_rename_a_league_with_members_and_invitations()
    {
        using var factory = new FixedClockApiFactory();
        var seed = await SeedLeagueWithMemberInviteAndLinkAsync(factory);

        var renamed = await ReadSuccessJsonAsync(await seed.Owner.PatchAsJsonAsync($"/api/leagues/{seed.LeagueId}/name", new { name = "After" }));

        Assert.Equal("After", renamed.GetProperty("name").GetString());
        Assert.Equal(2, renamed.GetProperty("memberCount").GetInt32());
        Assert.Equal("After", await factory.QueryDbAsync(db => db.Leagues.Where(l => l.Id == seed.LeagueId).Select(l => l.Name).SingleAsync()));
        Assert.Equal("owner", await factory.QueryDbAsync(db => db.Users.Where(u => u.Id == seed.Ids[0]).Select(u => u.Username).SingleAsync()));
    }

    [Fact]
    public async Task Member_who_is_not_the_owner_can_neither_rename_nor_delete_the_league()
    {
        using var factory = new FixedClockApiFactory();
        var seed = await SeedLeagueWithMemberInviteAndLinkAsync(factory);

        var rename = await seed.Member.PatchAsJsonAsync($"/api/leagues/{seed.LeagueId}/name", new { name = "x" });
        var delete = await seed.Member.DeleteAsync($"/api/leagues/{seed.LeagueId}");

        Assert.Equal(HttpStatusCode.Forbidden, rename.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Owner_can_delete_a_league_with_members_and_invitations()
    {
        using var factory = new FixedClockApiFactory();
        var seed = await SeedLeagueWithMemberInviteAndLinkAsync(factory);

        Assert.Equal(HttpStatusCode.NoContent, (await seed.Owner.DeleteAsync($"/api/leagues/{seed.LeagueId}")).StatusCode);

        Assert.False(await factory.QueryDbAsync(db => db.Leagues.AnyAsync()));
        Assert.False(await factory.QueryDbAsync(db => db.LeagueMembers.AnyAsync()));
        Assert.False(await factory.QueryDbAsync(db => db.LeagueInvitations.AnyAsync()));
        Assert.Equal(HttpStatusCode.NotFound, (await seed.Owner.DeleteAsync($"/api/leagues/{seed.LeagueId}")).StatusCode);
    }

    // ── Invitations ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Duplicate_pending_invite_is_409()
    {
        using var factory = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(factory, "owner", "bob");
        var owner = factory.CreateAuthenticatedClient(ids[0], "owner");
        var leagueId = await CreateLeagueAsync(owner);
        await InviteAsync(owner, leagueId, "bob");

        var again = await owner.PostAsJsonAsync($"/api/leagues/{leagueId}/invitations", new { username = "bob" });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    /// <summary>The filtered unique index backs the service check against concurrent invites.</summary>
    [Fact]
    public async Task Database_rejects_a_duplicate_pending_invite()
    {
        using var factory = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(factory, "owner", "bob");
        var owner = factory.CreateAuthenticatedClient(ids[0], "owner");
        var leagueId = await CreateLeagueAsync(owner);
        await InviteAsync(owner, leagueId, "bob");

        await factory.WithDbAsync(async db =>
        {
            db.LeagueInvitations.Add(new LeagueInvitation { LeagueId = leagueId, InviterId = ids[0], InviteeId = ids[1], Status = LeagueInvitationStatus.Pending });
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.True(ex.IsUniqueViolation());
        });

        Assert.Equal(1, await factory.QueryDbAsync(db => db.LeagueInvitations.CountAsync()));
    }

    private sealed record DeclinedInvite(HttpClient Owner, HttpClient Bob, int LeagueId, int InviteId);

    private static async Task<DeclinedInvite> DeclineAnInviteAsync(FixedClockApiFactory factory)
    {
        var ids = await SeedUsersAsync(factory, "owner", "bob");
        var owner = factory.CreateAuthenticatedClient(ids[0], "owner");
        var bob = factory.CreateAuthenticatedClient(ids[1], "bob");
        var leagueId = await CreateLeagueAsync(owner);
        var inviteId = await InviteAsync(owner, leagueId, "bob");
        Assert.Equal(HttpStatusCode.NoContent, (await bob.PostAsync($"/api/leagues/invitations/{inviteId}/decline", null)).StatusCode);
        return new DeclinedInvite(owner, bob, leagueId, inviteId);
    }

    [Fact]
    public async Task Declining_an_invite_that_is_no_longer_pending_is_409()
    {
        using var factory = new FixedClockApiFactory();
        var declined = await DeclineAnInviteAsync(factory);

        var again = await declined.Bob.PostAsync($"/api/leagues/invitations/{declined.InviteId}/decline", null);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Reinvite_after_a_decline_is_409_for_24_hours()
    {
        using var factory = new FixedClockApiFactory();
        var declined = await DeclineAnInviteAsync(factory);

        factory.Clock.Advance(TimeSpan.FromHours(23));
        var tooSoon = await declined.Owner.PostAsJsonAsync($"/api/leagues/{declined.LeagueId}/invitations", new { username = "bob" });
        Assert.Equal(HttpStatusCode.Conflict, tooSoon.StatusCode);

        factory.Clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1));
        await InviteAsync(declined.Owner, declined.LeagueId, "bob");
    }

    [Fact]
    public async Task Accepting_an_invite_twice_is_409()
    {
        using var factory = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(factory, "owner", "bob");
        var owner = factory.CreateAuthenticatedClient(ids[0], "owner");
        var bob = factory.CreateAuthenticatedClient(ids[1], "bob");
        var leagueId = await CreateLeagueAsync(owner);
        var inviteId = await InviteAsync(owner, leagueId, "bob");
        await ReadSuccessJsonAsync(await bob.PostAsync($"/api/leagues/invitations/{inviteId}/accept", null));

        var again = await bob.PostAsync($"/api/leagues/invitations/{inviteId}/accept", null);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Accepting_a_cancelled_invite_is_409()
    {
        using var factory = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(factory, "owner", "carol");
        var owner = factory.CreateAuthenticatedClient(ids[0], "owner");
        var carol = factory.CreateAuthenticatedClient(ids[1], "carol");
        var leagueId = await CreateLeagueAsync(owner);
        var cancelled = await InviteAsync(owner, leagueId, "carol");
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/leagues/invitations/{cancelled}")).StatusCode);

        var accept = await carol.PostAsync($"/api/leagues/invitations/{cancelled}/accept", null);

        Assert.Equal(HttpStatusCode.Conflict, accept.StatusCode);
    }

    /// <summary>Bob joins by invitation first, so he is a member when he uses the link.</summary>
    [Fact]
    public async Task Invite_link_is_used_up_by_the_first_non_member_but_not_by_a_member()
    {
        using var factory = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(factory, "owner", "bob", "carol", "dave");
        var owner = factory.CreateAuthenticatedClient(ids[0], "owner");
        var bob = factory.CreateAuthenticatedClient(ids[1], "bob");
        var carol = factory.CreateAuthenticatedClient(ids[2], "carol");
        var dave = factory.CreateAuthenticatedClient(ids[3], "dave");
        var leagueId = await CreateLeagueAsync(owner);
        var inviteId = await InviteAsync(owner, leagueId, "bob");
        await ReadSuccessJsonAsync(await bob.PostAsync($"/api/leagues/invitations/{inviteId}/accept", null));
        var token = (await ReadSuccessJsonAsync(await owner.PostAsync($"/api/leagues/{leagueId}/invite-link", null))).GetProperty("token").GetString();

        Assert.Equal(HttpStatusCode.Conflict, (await bob.PostAsync($"/api/leagues/join/{token}", null)).StatusCode);
        await ReadSuccessJsonAsync(await carol.PostAsync($"/api/leagues/join/{token}", null));
        Assert.Equal(HttpStatusCode.NotFound, (await dave.PostAsync($"/api/leagues/join/{token}", null)).StatusCode);

        Assert.Equal(3, await factory.QueryDbAsync(db => db.LeagueMembers.CountAsync(m => m.LeagueId == leagueId)));
        var notifications = await factory.QueryDbAsync(db => db.Notifications.Where(n => n.Type == "league_joined").Select(n => n.ActorUserId).ToListAsync());
        Assert.Equal(new int?[] { ids[1], ids[2] }, notifications.OrderBy(x => x));
    }

    // ── Game summaries ────────────────────────────────────────────────────────

    [Fact]
    public async Task Game_summaries_return_recentPlayDays_parallel_to_recentPlays()
    {
        using var factory = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(factory, "owner");
        var owner = factory.CreateAuthenticatedClient(ids[0], "owner");
        var leagueId = await CreateLeagueAsync(owner);
        var today = FixedClockApiFactory.Now.Date;
        await factory.WithDbAsync(async db =>
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

        var body = await ReadSuccessJsonAsync(await owner.GetAsync($"/api/leagues/{leagueId}/game-summaries?days=3"));
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
        using var factory = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(factory, "alice", "bob");
        var alice = factory.CreateAuthenticatedClient(ids[0], "alice");
        var bob = factory.CreateAuthenticatedClient(ids[1], "bob");

        await ReadSuccessJsonAsync(await alice.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[1] }));
        Assert.Equal(HttpStatusCode.Conflict, (await bob.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[0] })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await alice.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[1] })).StatusCode);
        Assert.Equal(1, await factory.QueryDbAsync(db => db.FriendRequests.CountAsync()));
    }

    [Fact]
    public async Task Declined_request_reads_as_none_and_can_be_resent_after_24_hours()
    {
        using var factory = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(factory, "alice", "bob");
        var alice = factory.CreateAuthenticatedClient(ids[0], "alice");
        var bob = factory.CreateAuthenticatedClient(ids[1], "bob");

        var requestId = (await ReadSuccessJsonAsync(await alice.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[1] }))).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await bob.PostAsync($"/api/friends/requests/{requestId}/reject", null)).StatusCode);

        Assert.Equal("none", await StatusAsync(alice, ids[1]));
        Assert.Equal("none", await StatusAsync(bob, ids[0]));
        Assert.Empty((await ReadSuccessJsonAsync(await bob.GetAsync("/api/friends/requests/incoming"))).EnumerateArray());

        factory.Clock.Advance(TimeSpan.FromHours(23));
        Assert.Equal(HttpStatusCode.Conflict, (await alice.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[1] })).StatusCode);

        factory.Clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1));
        var reopenedRequest = await ReadSuccessJsonAsync(await alice.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[1] }));
        Assert.Equal(requestId, reopenedRequest.GetProperty("id").GetInt32());
        Assert.Equal("pending_sent", await StatusAsync(alice, ids[1]));
        Assert.Equal("pending_received", await StatusAsync(bob, ids[0]));
        Assert.Equal(1, await factory.QueryDbAsync(db => db.FriendRequests.CountAsync()));
    }

    [Fact]
    public async Task The_user_who_declined_can_send_a_request_right_away()
    {
        using var factory = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(factory, "alice", "bob");
        var alice = factory.CreateAuthenticatedClient(ids[0], "alice");
        var bob = factory.CreateAuthenticatedClient(ids[1], "bob");

        var requestId = (await ReadSuccessJsonAsync(await alice.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[1] }))).GetProperty("id").GetInt32();
        await bob.PostAsync($"/api/friends/requests/{requestId}/reject", null);

        await ReadSuccessJsonAsync(await bob.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[0] }));
        Assert.Equal("pending_received", await StatusAsync(alice, ids[1]));
        await alice.PostAsync($"/api/friends/requests/{requestId}/accept", null);
        Assert.Equal("friends", await StatusAsync(alice, ids[1]));
    }

    private static async Task<string?> StatusAsync(HttpClient client, int targetUserId)
        => (await ReadSuccessJsonAsync(await client.GetAsync($"/api/friends/status/{targetUserId}"))).GetProperty("status").GetString();

    // ── Notifications ─────────────────────────────────────────────────────────

    /// <summary>
    /// "bobby sent you a friend request." contains "bob": the old substring check skipped bob's notification.
    /// bob then cancels and asks again within the cooldown, which must still leave one notification from bob.
    /// </summary>
    [Fact]
    public async Task Friend_request_notification_cooldown_matches_the_exact_sender()
    {
        using var factory = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(factory, "bobby", "bob", "carol");
        var bobby = factory.CreateAuthenticatedClient(ids[0], "bobby");
        var bob = factory.CreateAuthenticatedClient(ids[1], "bob");
        var carol = ids[2];

        await ReadSuccessJsonAsync(await bobby.PostAsJsonAsync("/api/friends/requests", new { targetUserId = carol }));
        var requestId = (await ReadSuccessJsonAsync(await bob.PostAsJsonAsync("/api/friends/requests", new { targetUserId = carol }))).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await bob.DeleteAsync($"/api/friends/requests/{requestId}")).StatusCode);
        await ReadSuccessJsonAsync(await bob.PostAsJsonAsync("/api/friends/requests", new { targetUserId = carol }));

        var page = await ReadSuccessJsonAsync(await factory.CreateAuthenticatedClient(carol, "carol").GetAsync("/api/notifications"));
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
        using var factory = new FixedClockApiFactory();
        var ids = await SeedUsersAsync(factory, "alice");
        var gameId = 0;
        await factory.WithDbAsync(async db =>
        {
            var game = new Game { Name = "G" };
            db.Games.Add(game);
            await db.SaveChangesAsync();
            gameId = game.Id;
        });
        var alice = factory.CreateAuthenticatedClient(ids[0], "alice");

        var first = await alice.PostAsync($"/api/favorites/{gameId}", null);
        var second = await alice.PostAsync($"/api/favorites/{gameId}", null);
        Assert.True(first.IsSuccessStatusCode);
        Assert.Equal(first.StatusCode, second.StatusCode);
        Assert.Equal(1, await factory.QueryDbAsync(db => db.Favorites.CountAsync()));
    }
}

/// <summary>Real concurrency: a file database, one connection per request (see <see cref="FileDbApiFactory"/>).</summary>
public class LeaguesAndSocialConcurrencyTests : IClassFixture<FileDbApiFactory>
{
    private readonly FileDbApiFactory _factory;

    public LeaguesAndSocialConcurrencyTests(FileDbApiFactory factory) => _factory = factory;

    private async Task<int[]> SeedUsersAsync(params string[] names)
    {
        var users = names.Select(n => new User { Username = $"{n}-{Guid.NewGuid():N}", PasswordHash = "x" }).ToList();
        await _factory.WithDbAsync(async db =>
        {
            db.Users.AddRange(users);
            await db.SaveChangesAsync();
        });
        return users.Select(u => u.Id).ToArray();
    }

    [Fact]
    public async Task Concurrent_double_accept_lets_exactly_one_through()
    {
        for (var round = 0; round < 5; round++)
        {
            var ids = await SeedUsersAsync("owner", "joiner");
            int leagueId = 0, invitationId = 0;
            await _factory.WithDbAsync(async db =>
            {
                var league = new League { Name = "L", OwnerId = ids[0], Members = { new LeagueMember { UserId = ids[0], Role = LeagueRole.Owner } } };
                db.Leagues.Add(league);
                await db.SaveChangesAsync();
                var invitation = new LeagueInvitation { LeagueId = league.Id, InviterId = ids[0], InviteeId = ids[1], Status = LeagueInvitationStatus.Pending };
                db.LeagueInvitations.Add(invitation);
                await db.SaveChangesAsync();
                (leagueId, invitationId) = (league.Id, invitation.Id);
            });

            var joiner = _factory.CreateAuthenticatedClient(ids[1], "joiner");
            var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
                joiner.PostAsync($"/api/leagues/invitations/{invitationId}/accept", null)));

            var codes = responses.Select(r => r.StatusCode).OrderBy(c => c).ToArray();
            Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, codes);
            Assert.Equal(2, await _factory.QueryDbAsync(db => db.LeagueMembers.CountAsync(m => m.LeagueId == leagueId)));
            Assert.Equal(1, await _factory.QueryDbAsync(db => db.Notifications.CountAsync(n => n.UserId == ids[0] && n.Type == "league_joined")));
        }
    }

    [Fact]
    public async Task Concurrent_friend_requests_in_both_directions_create_one_row()
    {
        var ids = await SeedUsersAsync("a", "b");
        var firstClient = _factory.CreateAuthenticatedClient(ids[0], "a");
        var secondClient = _factory.CreateAuthenticatedClient(ids[1], "b");

        var responses = await Task.WhenAll(
            firstClient.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[1] }),
            secondClient.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[0] }),
            firstClient.PostAsJsonAsync("/api/friends/requests", new { targetUserId = ids[1] }));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(1, await _factory.QueryDbAsync(db => db.FriendRequests.CountAsync(fr =>
            (fr.SenderId == ids[0] && fr.ReceiverId == ids[1]) || (fr.SenderId == ids[1] && fr.ReceiverId == ids[0]))));
    }

    [Fact]
    public async Task Concurrent_double_favorite_is_a_no_op()
    {
        var ids = await SeedUsersAsync("fav");
        var gameId = 0;
        await _factory.WithDbAsync(async db =>
        {
            var game = new Game { Name = "G" };
            db.Games.Add(game);
            await db.SaveChangesAsync();
            gameId = game.Id;
        });
        var client = _factory.CreateAuthenticatedClient(ids[0], "fav");

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostAsync($"/api/favorites/{gameId}", null)));

        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode, r.StatusCode.ToString()));
        Assert.Equal(1, await _factory.QueryDbAsync(db => db.Favorites.CountAsync(x => x.UserId == ids[0] && x.GameId == gameId)));
    }
}
