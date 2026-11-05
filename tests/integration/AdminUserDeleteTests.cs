using System.Net;
using System.Text;
using System.Text.Json;
using DailyChallenges.IntegrationTests.Infrastructure;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.IntegrationTests;

/// <summary>DELETE /api/admin/users/{id} with dependent rows (H7), plus the CSV export escaping.</summary>
public class AdminUserDeleteTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminUserDeleteTests(ApiFactory factory) => _factory = factory;

    private HttpClient Admin(int adminId = 800_001) => _factory.CreateAuthenticatedClient(adminId, "deleting-admin", isAdmin: true);

    private static async Task<string?> MessageAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("message").GetString();
    }

    [Fact]
    public async Task Deleting_a_user_with_friends_leagues_invites_and_submissions_succeeds()
    {
        var victim = new User { Username = "del-victim" };
        var requester = new User { Username = "del-requester" };   // sent the victim a friend request
        var friend = new User { Username = "del-friend" };         // received one from the victim
        var veteran = new User { Username = "del-veteran" };       // longest-standing member of the victim's league
        var newcomer = new User { Username = "del-newcomer" };
        var otherOwner = new User { Username = "del-other-owner" };
        var game = new Game { Name = "delete-test-game" };
        var day = new DateTime(2026, 5, 1);

        League shared = null!, solo = null!, foreign = null!;
        Submission submission = null!;
        await _factory.WithDbAsync(async db =>
        {
            db.Users.AddRange(victim, requester, friend, veteran, newcomer, otherOwner);
            db.Games.Add(game);
            await db.SaveChangesAsync();

            db.FriendRequests.AddRange(
                new FriendRequest { SenderId = requester.Id, ReceiverId = victim.Id, Status = FriendRequestStatus.Accepted },
                new FriendRequest { SenderId = victim.Id, ReceiverId = friend.Id });

            shared = new League { Name = "shared", OwnerId = victim.Id };
            solo = new League { Name = "solo", OwnerId = victim.Id };
            foreign = new League { Name = "foreign", OwnerId = otherOwner.Id };
            db.Leagues.AddRange(shared, solo, foreign);
            await db.SaveChangesAsync();

            db.LeagueMembers.AddRange(
                new LeagueMember { LeagueId = shared.Id, UserId = victim.Id, Role = LeagueRole.Owner, JoinedAt = day },
                new LeagueMember { LeagueId = shared.Id, UserId = newcomer.Id, JoinedAt = day.AddDays(5) },
                new LeagueMember { LeagueId = shared.Id, UserId = veteran.Id, JoinedAt = day.AddDays(1) },
                new LeagueMember { LeagueId = solo.Id, UserId = victim.Id, Role = LeagueRole.Owner, JoinedAt = day },
                new LeagueMember { LeagueId = foreign.Id, UserId = otherOwner.Id, Role = LeagueRole.Owner, JoinedAt = day });
            db.LeagueInvitations.AddRange(
                new LeagueInvitation { LeagueId = shared.Id, InviterId = victim.Id, InviteeId = friend.Id },        // sent
                new LeagueInvitation { LeagueId = shared.Id, InviterId = victim.Id, Token = "victim-link-token" },  // sent link
                new LeagueInvitation { LeagueId = foreign.Id, InviterId = otherOwner.Id, InviteeId = victim.Id });  // received

            submission = new Submission { GameId = game.Id, UserId = victim.Id, Username = "del-victim", Score = "42", ScoreValue = 42, ScoringDay = day };
            db.Submissions.Add(submission);
            await db.SaveChangesAsync();
        });

        var res = await Admin().DeleteAsync($"/api/admin/users/{victim.Id}");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        await _factory.WithDbAsync(async db =>
        {
            Assert.False(await db.Users.AnyAsync(u => u.Id == victim.Id));
            Assert.False(await db.FriendRequests.AnyAsync(f => f.SenderId == victim.Id || f.ReceiverId == victim.Id));
            Assert.False(await db.LeagueInvitations.AnyAsync(i => i.InviterId == victim.Id || i.InviteeId == victim.Id));

            // Shared league goes to the earliest-joined remaining member.
            var transferred = await db.Leagues.Include(l => l.Members).SingleAsync(l => l.Id == shared.Id);
            Assert.Equal(veteran.Id, transferred.OwnerId);
            Assert.Equal(LeagueRole.Owner, transferred.Members.Single(m => m.UserId == veteran.Id).Role);
            Assert.Equal(LeagueRole.Member, transferred.Members.Single(m => m.UserId == newcomer.Id).Role);
            Assert.DoesNotContain(transferred.Members, m => m.UserId == victim.Id);

            // The league with nobody else in it is gone; someone else's league is untouched.
            Assert.False(await db.Leagues.AnyAsync(l => l.Id == solo.Id));
            Assert.True(await db.Leagues.AnyAsync(l => l.Id == foreign.Id));

            // The submission stays, anonymized.
            var kept = await db.Submissions.AsNoTracking().SingleAsync(s => s.Id == submission.Id);
            Assert.Null(kept.UserId);
            Assert.Null(kept.Username);
        });
    }

    [Fact]
    public async Task Delete_checks_self_before_admin_and_protects_admins()
    {
        var otherAdmin = new User { Username = "del-admin", IsAdmin = true };
        await _factory.WithDbAsync(async db =>
        {
            db.Users.Add(otherAdmin);
            await db.SaveChangesAsync();
        });

        // An admin deleting themselves gets the self-delete message, not the generic admin one.
        var self = await _factory.CreateAuthenticatedClient(otherAdmin.Id, "del-admin", isAdmin: true).DeleteAsync($"/api/admin/users/{otherAdmin.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
        Assert.Equal("Cannot delete your own account", await MessageAsync(self));

        var other = await Admin().DeleteAsync($"/api/admin/users/{otherAdmin.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await Admin().DeleteAsync("/api/admin/users/987654")).StatusCode);
    }

    [Fact]
    public async Task Csv_export_neutralizes_formula_cells()
    {
        var user = new User { Username = "csv-user" };
        await _factory.WithDbAsync(async db =>
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
            db.XpEvents.AddRange(
                new XpEvent { UserId = user.Id, Amount = -5, EventType = "@SUM(A1)", Details = "=HYPERLINK(\"http://evil\")", CreatedAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc) },
                new XpEvent { UserId = user.Id, Amount = 5, EventType = "admin_adjustment", Details = "+1\t-2", CreatedAt = new DateTime(2026, 5, 2, 10, 0, 0, DateTimeKind.Utc) });
            await db.SaveChangesAsync();
        });

        var res = await Admin().GetAsync($"/api/admin/users/{user.Id}/xp-events/export");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var csv = Encoding.UTF8.GetString(await res.Content.ReadAsByteArrayAsync());
        Assert.Contains("\"'@SUM(A1)\"", csv);
        Assert.Contains("\"'=HYPERLINK(\"\"http://evil\"\")\"", csv);
        Assert.Contains("\"'+1\t-2\"", csv);
        Assert.Contains(",-5,", csv); // numeric cells are left alone
        Assert.Contains("\"admin_adjustment\"", csv);
    }
}
