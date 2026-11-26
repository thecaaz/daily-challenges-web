using System.Net;
using System.Text;
using DailyChallenges.IntegrationTests.Infrastructure;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
using static DailyChallenges.IntegrationTests.Infrastructure.HttpResponseJson;

namespace DailyChallenges.IntegrationTests;

/// <summary>DELETE /api/admin/users/{id} with dependent rows (H7), plus the CSV export escaping.</summary>
public class AdminUserDeleteTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminUserDeleteTests(ApiFactory factory) => _factory = factory;

    private HttpClient Admin(int adminId = 800_001) => _factory.CreateAuthenticatedClient(adminId, "deleting-admin", isAdmin: true);

    private static async Task<string?> MessageAsync(HttpResponseMessage res) =>
        (await ReadJsonAsync(res)).GetProperty("message").GetString();

    private sealed record VictimSeed(User Victim, User LongestStandingMember, User Newcomer, League Shared, League Solo, League Foreign, Submission Submission);

    /// <summary>
    /// A user ("victim") with an accepted friend request received, a pending one sent, a league shared with two members
    /// (the longest-standing joined first), a league of their own, invitations sent (direct and link) and received,
    /// and a submission. All names start with <paramref name="prefix"/>, since the tests share one database.
    /// </summary>
    private async Task<VictimSeed> SeedVictimAsync(string prefix)
    {
        var victim = new User { Username = $"{prefix}-victim" };
        var friendRequestSender = new User { Username = $"{prefix}-requester" };
        var friendRequestReceiver = new User { Username = $"{prefix}-friend" };
        var longestStandingMember = new User { Username = $"{prefix}-veteran" };
        var newcomer = new User { Username = $"{prefix}-newcomer" };
        var otherOwner = new User { Username = $"{prefix}-other-owner" };
        var game = new Game { Name = $"{prefix}-game" };
        var day = new DateTime(2026, 5, 1);

        VictimSeed seed = null!;
        await _factory.WithDbAsync(async db =>
        {
            db.Users.AddRange(victim, friendRequestSender, friendRequestReceiver, longestStandingMember, newcomer, otherOwner);
            db.Games.Add(game);
            await db.SaveChangesAsync();

            db.FriendRequests.AddRange(
                new FriendRequest { SenderId = friendRequestSender.Id, ReceiverId = victim.Id, Status = FriendRequestStatus.Accepted },
                new FriendRequest { SenderId = victim.Id, ReceiverId = friendRequestReceiver.Id });

            var shared = new League { Name = $"{prefix}-shared", OwnerId = victim.Id };
            var solo = new League { Name = $"{prefix}-solo", OwnerId = victim.Id };
            var foreign = new League { Name = $"{prefix}-foreign", OwnerId = otherOwner.Id };
            db.Leagues.AddRange(shared, solo, foreign);
            await db.SaveChangesAsync();

            db.LeagueMembers.AddRange(
                new LeagueMember { LeagueId = shared.Id, UserId = victim.Id, Role = LeagueRole.Owner, JoinedAt = day },
                new LeagueMember { LeagueId = shared.Id, UserId = newcomer.Id, JoinedAt = day.AddDays(5) },
                new LeagueMember { LeagueId = shared.Id, UserId = longestStandingMember.Id, JoinedAt = day.AddDays(1) },
                new LeagueMember { LeagueId = solo.Id, UserId = victim.Id, Role = LeagueRole.Owner, JoinedAt = day },
                new LeagueMember { LeagueId = foreign.Id, UserId = otherOwner.Id, Role = LeagueRole.Owner, JoinedAt = day });
            var sentInvite = new LeagueInvitation { LeagueId = shared.Id, InviterId = victim.Id, InviteeId = friendRequestReceiver.Id };
            var sentLink = new LeagueInvitation { LeagueId = shared.Id, InviterId = victim.Id, Token = $"{prefix}-link-token" };
            var receivedInvite = new LeagueInvitation { LeagueId = foreign.Id, InviterId = otherOwner.Id, InviteeId = victim.Id };
            db.LeagueInvitations.AddRange(sentInvite, sentLink, receivedInvite);

            var submission = new Submission { GameId = game.Id, UserId = victim.Id, Username = victim.Username, Score = "42", ScoreValue = 42, ScoringDay = day };
            db.Submissions.Add(submission);
            await db.SaveChangesAsync();
            seed = new VictimSeed(victim, longestStandingMember, newcomer, shared, solo, foreign, submission);
        });
        return seed;
    }

    private async Task DeleteAsAdminAsync(User user)
    {
        var res = await Admin().DeleteAsync($"/api/admin/users/{user.Id}");
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_user_removes_them_with_their_friend_requests_and_invitations()
    {
        var seed = await SeedVictimAsync("del-rows");
        var victimId = seed.Victim.Id;

        await DeleteAsAdminAsync(seed.Victim);

        await _factory.WithDbAsync(async db =>
        {
            Assert.False(await db.Users.AnyAsync(u => u.Id == victimId));
            Assert.False(await db.FriendRequests.AnyAsync(f => f.SenderId == victimId || f.ReceiverId == victimId));
            Assert.False(await db.LeagueInvitations.AnyAsync(i => i.InviterId == victimId || i.InviteeId == victimId));
        });
    }

    [Fact]
    public async Task Deleting_a_league_owner_hands_a_shared_league_to_the_earliest_joined_member()
    {
        var seed = await SeedVictimAsync("del-shared");

        await DeleteAsAdminAsync(seed.Victim);

        await _factory.WithDbAsync(async db =>
        {
            var transferred = await db.Leagues.Include(l => l.Members).SingleAsync(l => l.Id == seed.Shared.Id);
            Assert.Equal(seed.LongestStandingMember.Id, transferred.OwnerId);
            Assert.Equal(LeagueRole.Owner, transferred.Members.Single(m => m.UserId == seed.LongestStandingMember.Id).Role);
            Assert.Equal(LeagueRole.Member, transferred.Members.Single(m => m.UserId == seed.Newcomer.Id).Role);
            Assert.DoesNotContain(transferred.Members, m => m.UserId == seed.Victim.Id);
        });
    }

    [Fact]
    public async Task Deleting_a_league_owner_removes_a_league_with_nobody_else_and_keeps_other_owners_leagues()
    {
        var seed = await SeedVictimAsync("del-solo");

        await DeleteAsAdminAsync(seed.Victim);

        await _factory.WithDbAsync(async db =>
        {
            Assert.False(await db.Leagues.AnyAsync(l => l.Id == seed.Solo.Id));
            Assert.True(await db.Leagues.AnyAsync(l => l.Id == seed.Foreign.Id));
        });
    }

    [Fact]
    public async Task Deleting_a_user_keeps_their_submissions_anonymized()
    {
        var seed = await SeedVictimAsync("del-subs");

        await DeleteAsAdminAsync(seed.Victim);

        await _factory.WithDbAsync(async db =>
        {
            var kept = await db.Submissions.AsNoTracking().SingleAsync(s => s.Id == seed.Submission.Id);
            Assert.Null(kept.UserId);
            Assert.Null(kept.Username);
        });
    }

    private async Task<User> SeedAdminAsync(string username)
    {
        var admin = new User { Username = username, IsAdmin = true };
        await _factory.WithDbAsync(async db =>
        {
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        });
        return admin;
    }

    /// <summary>The self check runs before the admin check, so the message is not the generic admin one.</summary>
    [Fact]
    public async Task Admin_deleting_themselves_gets_the_self_delete_message()
    {
        var admin = await SeedAdminAsync("del-self-admin");

        var self = await _factory.CreateAuthenticatedClient(admin.Id, admin.Username, isAdmin: true).DeleteAsync($"/api/admin/users/{admin.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
        Assert.Equal("Cannot delete your own account", await MessageAsync(self));
    }

    [Fact]
    public async Task Deleting_another_admin_is_forbidden()
    {
        var otherAdmin = await SeedAdminAsync("del-other-admin");

        var res = await Admin().DeleteAsync($"/api/admin/users/{otherAdmin.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_missing_user_is_404()
    {
        const int missingUserId = 987654;

        var res = await Admin().DeleteAsync($"/api/admin/users/{missingUserId}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    /// <summary>Exports the XP events of a new user with formula-like event types and details.</summary>
    private async Task<string> ExportCsvWithFormulaLikeCellsAsync(string username)
    {
        var user = new User { Username = username };
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
        return Encoding.UTF8.GetString(await res.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Csv_export_neutralizes_formula_cells()
    {
        var csv = await ExportCsvWithFormulaLikeCellsAsync("csv-user");

        Assert.Contains("\"'@SUM(A1)\"", csv);
        Assert.Contains("\"'=HYPERLINK(\"\"http://evil\"\")\"", csv);
        Assert.Contains("\"'+1\t-2\"", csv);
        Assert.Contains("\"admin_adjustment\"", csv);
    }

    [Fact]
    public async Task Csv_export_leaves_numeric_cells_alone()
    {
        var csv = await ExportCsvWithFormulaLikeCellsAsync("csv-numeric-user");

        Assert.Contains(",-5,", csv);
    }
}
