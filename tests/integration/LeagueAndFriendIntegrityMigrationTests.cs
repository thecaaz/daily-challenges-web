using DailyChallenges.Data;
using DailyChallenges.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DailyChallenges.IntegrationTests;

public class LeagueAndFriendIntegrityMigrationTests
{
    private const string PreviousMigration = "20261104225341_UniqueUsernamesAndTokenRevocation";

    [Fact]
    public void Existing_duplicates_are_resolved_before_the_unique_indexes()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.GetService<IMigrator>().Migrate(PreviousMigration);

        db.Database.ExecuteSqlRaw(
            "INSERT INTO Users (Id, Username, PasswordHash, IsAdmin, TotalXp, Level, Streak, TokenVersion) VALUES " +
            " (1, 'owner', 'x', 1, 0, 1, 0, 0), (2, 'bob', 'x', 0, 0, 1, 0, 0), (3, 'carol', 'x', 0, 0, 1, 0, 0)," +
            " (4, 'dave', 'x', 0, 0, 1, 0, 0), (5, 'erin', 'x', 0, 0, 1, 0, 0);" +
            "INSERT INTO Leagues (Id, Name, OwnerId, CreatedAt) VALUES (1, 'L', 1, '2026-01-01 00:00:00');" +
            "INSERT INTO LeagueInvitations (Id, LeagueId, InviterId, InviteeId, Token, Status, CreatedAt, UpdatedAt) VALUES " +
            // duplicate pending direct invites for bob: 1 stays, 2 is cancelled
            " (1, 1, 1, 2, NULL, 0, '2026-01-01 00:00:00', NULL), (2, 1, 1, 2, NULL, 0, '2026-01-02 00:00:00', NULL)," +
            // carol: a declined and a pending invite do not collide
            " (3, 1, 1, 3, NULL, 2, '2026-01-01 00:00:00', '2026-01-01 01:00:00'), (4, 1, 1, 3, NULL, 0, '2026-01-03 00:00:00', NULL)," +
            // duplicate link tokens: 5 keeps it; 6 (pending) is cancelled; 7 (accepted) only loses the token
            " (5, 1, 1, NULL, 'tok', 0, '2026-01-01 00:00:00', NULL), (6, 1, 1, NULL, 'tok', 0, '2026-01-02 00:00:00', NULL)," +
            " (7, 1, 1, NULL, 'tok', 1, '2026-01-03 00:00:00', '2026-01-03 01:00:00');" +
            "INSERT INTO FriendRequests (Id, SenderId, ReceiverId, Status, CreatedAt) VALUES " +
            // pair (1,2): the accepted row wins over the older pending one
            " (1, 1, 2, 0, '2026-01-01 00:00:00'), (2, 2, 1, 1, '2026-01-02 00:00:00')," +
            // pair (1,3): both pending, the oldest wins
            " (3, 1, 3, 0, '2026-01-02 00:00:00'), (4, 3, 1, 0, '2026-01-01 00:00:00')," +
            // pair (4,5): no duplicate
            " (5, 4, 5, 0, '2026-01-01 00:00:00');" +
            "INSERT INTO Notifications (Id, UserId, GameId, ScoringDay, Message, Type, Rank, IsRead, CreatedAt) VALUES " +
            " (1, 1, NULL, '2026-01-01 00:00:00', 'bob sent you a friend request.', 'friend_request', NULL, 0, '2026-01-01 00:00:00')," +
            " (2, 1, NULL, '2026-01-01 00:00:00', 'carol joined your league \"L\".', 'league_joined', NULL, 0, '2026-01-01 00:00:00')," +
            " (3, 2, NULL, '2026-01-01 00:00:00', 'owner invited you to join the league \"L\".', 'league_invite', NULL, 0, '2026-01-01 00:00:00')," +
            " (4, 2, NULL, '2026-01-01 00:00:00', 'gone accepted your friend request.', 'friend_request_accepted', NULL, 0, '2026-01-01 00:00:00')," +
            " (5, 2, NULL, '2026-01-01 00:00:00', 'You won!', 'day_win', 1, 0, '2026-01-01 00:00:00');");

        db.Database.Migrate();

        var invites = db.LeagueInvitations.AsNoTracking().OrderBy(i => i.Id).ToDictionary(i => i.Id);
        Assert.Equal(LeagueInvitationStatus.Pending, invites[1].Status);
        Assert.Equal(LeagueInvitationStatus.Cancelled, invites[2].Status);
        Assert.NotNull(invites[2].UpdatedAt);
        Assert.Equal(LeagueInvitationStatus.Declined, invites[3].Status);
        Assert.Equal(LeagueInvitationStatus.Pending, invites[4].Status);
        Assert.Equal(("tok", LeagueInvitationStatus.Pending), (invites[5].Token, invites[5].Status));
        Assert.Equal((null, LeagueInvitationStatus.Cancelled), (invites[6].Token, invites[6].Status));
        Assert.Equal((null, LeagueInvitationStatus.Accepted), (invites[7].Token, invites[7].Status));

        Assert.Equal(new[] { 2, 4, 5 }, db.FriendRequests.AsNoTracking().OrderBy(f => f.Id).Select(f => f.Id).ToArray());

        var actors = db.Notifications.AsNoTracking().OrderBy(n => n.Id).Select(n => n.ActorUserId).ToArray();
        Assert.Equal(new int?[] { 2, 3, 1, null, null }, actors);

        // The new unique indexes are in place.
        AssertUniqueViolation(() => db.Database.ExecuteSqlRaw(
            "INSERT INTO FriendRequests (SenderId, ReceiverId, Status, CreatedAt) VALUES (5, 4, 0, '2026-02-01 00:00:00');"));
        AssertUniqueViolation(() => db.Database.ExecuteSqlRaw(
            "INSERT INTO LeagueInvitations (LeagueId, InviterId, InviteeId, Status, CreatedAt) VALUES (1, 1, 2, 0, '2026-02-01 00:00:00');"));
        AssertUniqueViolation(() => db.Database.ExecuteSqlRaw(
            "INSERT INTO LeagueInvitations (LeagueId, InviterId, Token, Status, CreatedAt) VALUES (1, 1, 'tok', 3, '2026-02-01 00:00:00');"));
        // Non-pending invites for the same invitee are still allowed.
        db.Database.ExecuteSqlRaw(
            "INSERT INTO LeagueInvitations (LeagueId, InviterId, InviteeId, Status, CreatedAt) VALUES (1, 1, 2, 2, '2026-02-01 00:00:00');");

        // Deleting the actor keeps the notification and clears the actor.
        db.Database.ExecuteSqlRaw("DELETE FROM FriendRequests WHERE SenderId = 3 OR ReceiverId = 3; DELETE FROM LeagueInvitations WHERE InviteeId = 3; DELETE FROM Users WHERE Id = 3;");
        Assert.Null(db.Notifications.AsNoTracking().Single(n => n.Id == 2).ActorUserId);
    }

    private static void AssertUniqueViolation(Action action)
    {
        var ex = Assert.Throws<SqliteException>(action);
        Assert.Equal(19, ex.SqliteErrorCode);
        Assert.Equal(2067, ex.SqliteExtendedErrorCode);
    }
}
