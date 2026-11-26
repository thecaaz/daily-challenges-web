using DailyChallenges.IntegrationTests.Infrastructure;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.IntegrationTests;

/// <summary>
/// The LeagueAndFriendIntegrity migration resolves duplicate invitations and friend requests before it adds
/// unique indexes, and backfills the actor of social notifications.
/// </summary>
public class LeagueAndFriendIntegrityMigrationTests
{
    private const string PreviousMigration = "20261104225341_UniqueUsernamesAndTokenRevocation";

    private const string Users =
        "INSERT INTO Users (Id, Username, PasswordHash, IsAdmin, TotalXp, Level, Streak, TokenVersion) VALUES " +
        " (1, 'owner', 'x', 1, 0, 1, 0, 0), (2, 'bob', 'x', 0, 0, 1, 0, 0), (3, 'carol', 'x', 0, 0, 1, 0, 0)," +
        " (4, 'dave', 'x', 0, 0, 1, 0, 0), (5, 'erin', 'x', 0, 0, 1, 0, 0);" +
        "INSERT INTO Leagues (Id, Name, OwnerId, CreatedAt) VALUES (1, 'L', 1, '2026-01-01 00:00:00');";

    private const string InvitationColumns =
        "INSERT INTO LeagueInvitations (Id, LeagueId, InviterId, InviteeId, Token, Status, CreatedAt, UpdatedAt) VALUES ";

    /// <summary>Invitations 1 and 2: both pending for bob.</summary>
    private const string DuplicatePendingInvitesForBob =
        " (1, 1, 1, 2, NULL, 0, '2026-01-01 00:00:00', NULL), (2, 1, 1, 2, NULL, 0, '2026-01-02 00:00:00', NULL)";

    /// <summary>Invitations 3 (declined) and 4 (pending) for carol, which do not collide.</summary>
    private const string DeclinedAndPendingInvitesForCarol =
        " (3, 1, 1, 3, NULL, 2, '2026-01-01 00:00:00', '2026-01-01 01:00:00'), (4, 1, 1, 3, NULL, 0, '2026-01-03 00:00:00', NULL)";

    /// <summary>Invitations 5 (pending), 6 (pending) and 7 (accepted) share the link token "tok".</summary>
    private const string DuplicateLinkTokens =
        " (5, 1, 1, NULL, 'tok', 0, '2026-01-01 00:00:00', NULL), (6, 1, 1, NULL, 'tok', 0, '2026-01-02 00:00:00', NULL)," +
        " (7, 1, 1, NULL, 'tok', 1, '2026-01-03 00:00:00', '2026-01-03 01:00:00')";

    private const string FriendRequestColumns = "INSERT INTO FriendRequests (Id, SenderId, ReceiverId, Status, CreatedAt) VALUES ";

    /// <summary>Requests 1 (pending, older) and 2 (accepted) between users 1 and 2.</summary>
    private const string PendingAndAcceptedRequestsForOnePair =
        " (1, 1, 2, 0, '2026-01-01 00:00:00'), (2, 2, 1, 1, '2026-01-02 00:00:00')";

    /// <summary>Requests 3 (newer) and 4 (older) between users 1 and 3, both pending.</summary>
    private const string TwoPendingRequestsForOnePair =
        " (3, 1, 3, 0, '2026-01-02 00:00:00'), (4, 3, 1, 0, '2026-01-01 00:00:00')";

    /// <summary>Request 5 between users 4 and 5, the only one for its pair.</summary>
    private const string UniqueRequest = " (5, 4, 5, 0, '2026-01-01 00:00:00')";

    private const string Notifications =
        "INSERT INTO Notifications (Id, UserId, GameId, ScoringDay, Message, Type, Rank, IsRead, CreatedAt) VALUES " +
        " (1, 1, NULL, '2026-01-01 00:00:00', 'bob sent you a friend request.', 'friend_request', NULL, 0, '2026-01-01 00:00:00')," +
        " (2, 1, NULL, '2026-01-01 00:00:00', 'carol joined your league \"L\".', 'league_joined', NULL, 0, '2026-01-01 00:00:00')," +
        " (3, 2, NULL, '2026-01-01 00:00:00', 'owner invited you to join the league \"L\".', 'league_invite', NULL, 0, '2026-01-01 00:00:00')," +
        " (4, 2, NULL, '2026-01-01 00:00:00', 'gone accepted your friend request.', 'friend_request_accepted', NULL, 0, '2026-01-01 00:00:00')," +
        " (5, 2, NULL, '2026-01-01 00:00:00', 'You won!', 'day_win', 1, 0, '2026-01-01 00:00:00');";

    /// <summary>Seeds the duplicates above on the previous schema, then applies the migration.</summary>
    private static MigrationTestDb MigrateWithDuplicates()
    {
        var db = MigrationTestDb.MigratedTo(PreviousMigration);
        db.ExecuteSql(
            Users +
            InvitationColumns + string.Join(",", DuplicatePendingInvitesForBob, DeclinedAndPendingInvitesForCarol, DuplicateLinkTokens) + ";" +
            FriendRequestColumns + string.Join(",", PendingAndAcceptedRequestsForOnePair, TwoPendingRequestsForOnePair, UniqueRequest) + ";" +
            Notifications);
        db.MigrateToLatest();
        return db;
    }

    private static Dictionary<int, LeagueInvitation> InvitationsById(MigrationTestDb db) =>
        db.Context.LeagueInvitations.AsNoTracking().ToDictionary(i => i.Id);

    [Fact]
    public void Duplicate_pending_invites_for_one_invitee_keep_the_oldest_and_cancel_the_rest()
    {
        using var db = MigrateWithDuplicates();

        var invites = InvitationsById(db);

        Assert.Equal(LeagueInvitationStatus.Pending, invites[1].Status);
        Assert.Equal(LeagueInvitationStatus.Cancelled, invites[2].Status);
        Assert.NotNull(invites[2].UpdatedAt);
    }

    [Fact]
    public void Declined_and_pending_invites_for_one_invitee_are_both_kept()
    {
        using var db = MigrateWithDuplicates();

        var invites = InvitationsById(db);

        Assert.Equal(LeagueInvitationStatus.Declined, invites[3].Status);
        Assert.Equal(LeagueInvitationStatus.Pending, invites[4].Status);
    }

    [Fact]
    public void Duplicate_link_tokens_stay_on_the_oldest_invite_and_later_pending_ones_are_cancelled()
    {
        using var db = MigrateWithDuplicates();

        var invites = InvitationsById(db);

        Assert.Equal(("tok", LeagueInvitationStatus.Pending), (invites[5].Token, invites[5].Status));
        Assert.Equal((null, LeagueInvitationStatus.Cancelled), (invites[6].Token, invites[6].Status));
        Assert.Equal((null, LeagueInvitationStatus.Accepted), (invites[7].Token, invites[7].Status));
    }

    [Fact]
    public void Duplicate_friend_requests_keep_the_accepted_row_or_else_the_oldest()
    {
        using var db = MigrateWithDuplicates();

        var remaining = db.Context.FriendRequests.AsNoTracking().OrderBy(f => f.Id).Select(f => f.Id).ToArray();

        Assert.Equal(new[] { 2, 4, 5 }, remaining);
    }

    [Fact]
    public void Social_notifications_get_their_actor_backfilled_from_the_message()
    {
        using var db = MigrateWithDuplicates();

        var actors = db.Context.Notifications.AsNoTracking().OrderBy(n => n.Id).Select(n => n.ActorUserId).ToArray();

        Assert.Equal(new int?[] { 2, 3, 1, null, null }, actors);
    }

    [Fact]
    public void Unique_indexes_reject_new_duplicate_requests_invites_and_tokens()
    {
        using var db = MigrateWithDuplicates();

        SqliteAssert.UniqueViolation(() => db.ExecuteSql(
            "INSERT INTO FriendRequests (SenderId, ReceiverId, Status, CreatedAt) VALUES (5, 4, 0, '2026-02-01 00:00:00');"));
        SqliteAssert.UniqueViolation(() => db.ExecuteSql(
            "INSERT INTO LeagueInvitations (LeagueId, InviterId, InviteeId, Status, CreatedAt) VALUES (1, 1, 2, 0, '2026-02-01 00:00:00');"));
        SqliteAssert.UniqueViolation(() => db.ExecuteSql(
            "INSERT INTO LeagueInvitations (LeagueId, InviterId, Token, Status, CreatedAt) VALUES (1, 1, 'tok', 3, '2026-02-01 00:00:00');"));
    }

    [Fact]
    public void Non_pending_invites_for_an_invitee_with_a_pending_invite_are_still_allowed()
    {
        using var db = MigrateWithDuplicates();
        var countBefore = db.Context.LeagueInvitations.Count();

        db.ExecuteSql(
            "INSERT INTO LeagueInvitations (LeagueId, InviterId, InviteeId, Status, CreatedAt) VALUES (1, 1, 2, 2, '2026-02-01 00:00:00');");

        Assert.Equal(countBefore + 1, db.Context.LeagueInvitations.Count());
    }

    [Fact]
    public void Deleting_the_actor_keeps_the_notification_and_clears_the_actor()
    {
        using var db = MigrateWithDuplicates();

        db.ExecuteSql("DELETE FROM FriendRequests WHERE SenderId = 3 OR ReceiverId = 3; DELETE FROM LeagueInvitations WHERE InviteeId = 3; DELETE FROM Users WHERE Id = 3;");

        Assert.Null(db.Context.Notifications.AsNoTracking().Single(n => n.Id == 2).ActorUserId);
    }
}
