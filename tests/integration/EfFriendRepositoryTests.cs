using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.IntegrationTests;

public class EfFriendRepositoryTests
{
    private static void SeedUser(AppDbContext ctx, int id)
    {
        ctx.Users.Add(new User { Id = id, Username = $"user{id}" });
        ctx.SaveChanges();
    }

    private static FriendRequest SeedFriendRequest(
        AppDbContext ctx, int senderId, int receiverId,
        FriendRequestStatus status = FriendRequestStatus.Pending)
    {
        var fr = new FriendRequest { SenderId = senderId, ReceiverId = receiverId, Status = status };
        ctx.FriendRequests.Add(fr);
        ctx.SaveChanges();
        return fr;
    }

    // ── GetExistingAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetExisting_FindsForwardDirection()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedUser(db.Ctx, 2);
        SeedFriendRequest(db.Ctx, 1, 2);
        var repo = new EfFriendRepository(db.Ctx);

        var result = await repo.GetExistingAsync(1, 2);

        Assert.NotNull(result);
        Assert.Equal(1, result!.SenderId);
        Assert.Equal(2, result.ReceiverId);
    }

    [Fact]
    public async Task GetExisting_FindsReverseDirection()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedUser(db.Ctx, 2);
        SeedFriendRequest(db.Ctx, 1, 2);
        var repo = new EfFriendRepository(db.Ctx);

        var result = await repo.GetExistingAsync(2, 1); // queried in reverse

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetExisting_ReturnsNull_WhenNone()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedUser(db.Ctx, 2);
        var repo = new EfFriendRepository(db.Ctx);

        var result = await repo.GetExistingAsync(1, 2);

        Assert.Null(result);
    }

    // ── GetIncomingPendingAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetIncomingPending_OnlyReturnsReceiversPendingRequests()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedUser(db.Ctx, 2);
        SeedUser(db.Ctx, 3);
        SeedUser(db.Ctx, 4);
        SeedFriendRequest(db.Ctx, 1, 2);  // incoming for user 2
        SeedFriendRequest(db.Ctx, 3, 2);  // incoming for user 2
        SeedFriendRequest(db.Ctx, 2, 4);  // outgoing from user 2 — excluded
        var repo = new EfFriendRepository(db.Ctx);

        var results = await repo.GetIncomingPendingAsync(2);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(2, r.ReceiverId));
    }

    [Fact]
    public async Task GetIncomingPending_ExcludesAccepted()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedUser(db.Ctx, 2);
        SeedFriendRequest(db.Ctx, 1, 2, FriendRequestStatus.Accepted);
        var repo = new EfFriendRepository(db.Ctx);

        var results = await repo.GetIncomingPendingAsync(2);

        Assert.Empty(results);
    }

    // ── GetOutgoingPendingAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetOutgoingPending_OnlyReturnsSendersPendingRequests()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedUser(db.Ctx, 2);
        SeedUser(db.Ctx, 3);
        SeedUser(db.Ctx, 4);
        SeedUser(db.Ctx, 5);
        SeedFriendRequest(db.Ctx, 1, 2);  // outgoing from user 1
        SeedFriendRequest(db.Ctx, 1, 3);  // outgoing from user 1
        SeedFriendRequest(db.Ctx, 4, 1);  // incoming to user 1 — excluded
        SeedFriendRequest(db.Ctx, 1, 5, FriendRequestStatus.Declined);  // declined — excluded
        var repo = new EfFriendRepository(db.Ctx);

        var results = await repo.GetOutgoingPendingAsync(1);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(1, r.SenderId));
    }

    // ── GetFriendsAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetFriends_ReturnsFriendUsers_BothDirections()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedUser(db.Ctx, 2); // user 1 sent to user 2
        SeedUser(db.Ctx, 3); // user 3 sent to user 1
        SeedFriendRequest(db.Ctx, 1, 2, FriendRequestStatus.Accepted);
        SeedFriendRequest(db.Ctx, 3, 1, FriendRequestStatus.Accepted);
        var repo = new EfFriendRepository(db.Ctx);

        var friends = await repo.GetFriendsAsync(1);

        Assert.Equal(2, friends.Count);
        Assert.Contains(friends, u => u.Id == 2);
        Assert.Contains(friends, u => u.Id == 3);
    }

    [Fact]
    public async Task GetFriends_ExcludesPendingRequests()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedUser(db.Ctx, 2);
        SeedFriendRequest(db.Ctx, 1, 2, FriendRequestStatus.Pending);
        var repo = new EfFriendRepository(db.Ctx);

        var friends = await repo.GetFriendsAsync(1);

        Assert.Empty(friends);
    }

    // ── HasAcceptedFriendAsync ────────────────────────────────────────────────

    [Fact]
    public async Task HasAcceptedFriend_True_WhenAcceptedExists()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedUser(db.Ctx, 2);
        SeedFriendRequest(db.Ctx, 1, 2, FriendRequestStatus.Accepted);
        var repo = new EfFriendRepository(db.Ctx);

        Assert.True(await repo.HasAcceptedFriendAsync(1));
    }

    [Fact]
    public async Task HasAcceptedFriend_True_WhenUserIsReceiver()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedUser(db.Ctx, 2);
        SeedFriendRequest(db.Ctx, 2, 1, FriendRequestStatus.Accepted); // user 1 is receiver
        var repo = new EfFriendRepository(db.Ctx);

        Assert.True(await repo.HasAcceptedFriendAsync(1));
    }

    [Fact]
    public async Task HasAcceptedFriend_False_WhenOnlyPending()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedUser(db.Ctx, 2);
        SeedFriendRequest(db.Ctx, 1, 2, FriendRequestStatus.Pending);
        var repo = new EfFriendRepository(db.Ctx);

        Assert.False(await repo.HasAcceptedFriendAsync(1));
    }

    [Fact]
    public async Task HasAcceptedFriend_False_WhenNoRequests()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        var repo = new EfFriendRepository(db.Ctx);

        Assert.False(await repo.HasAcceptedFriendAsync(1));
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_RemovesFriendRequest()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedUser(db.Ctx, 2);
        var fr = SeedFriendRequest(db.Ctx, 1, 2);
        var repo = new EfFriendRepository(db.Ctx);

        await repo.DeleteAsync(fr.Id);

        Assert.Equal(0, db.Ctx.FriendRequests.Count());
    }

    [Fact]
    public async Task Delete_NonExistentId_DoesNotThrow()
    {
        await using var db = new TestDb();
        var repo = new EfFriendRepository(db.Ctx);

        await repo.DeleteAsync(9999); // no-op expected
    }

    // ── One row per pair; Declined ────────────────────────────────────────────

    [Fact]
    public async Task Reverse_direction_insert_is_rejected_by_the_pair_index()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedUser(db.Ctx, 2);
        SeedFriendRequest(db.Ctx, 1, 2);
        var repo = new EfFriendRepository(db.Ctx);

        var ex = await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() =>
            repo.CreateAsync(new FriendRequest { SenderId = 2, ReceiverId = 1, Status = FriendRequestStatus.Pending }));
        Assert.True(ex.IsUniqueViolation());

        // The failed insert was detached, so the context still saves.
        SeedUser(db.Ctx, 3);
        Assert.Equal(1, db.Ctx.FriendRequests.Count());
    }

    [Fact]
    public async Task Declined_requests_are_not_friends_or_pending()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedUser(db.Ctx, 2);
        SeedFriendRequest(db.Ctx, 1, 2, FriendRequestStatus.Declined);
        var repo = new EfFriendRepository(db.Ctx);

        Assert.Empty(await repo.GetIncomingPendingAsync(2));
        Assert.Empty(await repo.GetOutgoingPendingAsync(1));
        Assert.Empty(await repo.GetFriendsAsync(1));
        Assert.False(await repo.HasAcceptedFriendAsync(1));
    }

    [Fact]
    public async Task ReopenDeclined_turns_the_row_into_a_new_pending_request_in_the_new_direction()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedUser(db.Ctx, 2);
        var fr = SeedFriendRequest(db.Ctx, 1, 2, FriendRequestStatus.Declined);
        var repo = new EfFriendRepository(db.Ctx);
        var now = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(await repo.ReopenDeclinedAsync(fr.Id, 2, 1, now));
        Assert.False(await repo.ReopenDeclinedAsync(fr.Id, 2, 1, now)); // no longer Declined

        var reopened = await repo.GetByIdAsync(fr.Id);
        Assert.Equal((2, 1, FriendRequestStatus.Pending, now), (reopened!.SenderId, reopened.ReceiverId, reopened.Status, reopened.CreatedAt));
        Assert.Null(reopened.UpdatedAt);
    }

    [Fact]
    public async Task Update_does_not_write_back_the_loaded_users()
    {
        await using var db = new TestDb();
        SeedUser(db.Ctx, 1);
        SeedUser(db.Ctx, 2);
        var seeded = SeedFriendRequest(db.Ctx, 1, 2);
        db.Ctx.ChangeTracker.Clear();
        var repo = new EfFriendRepository(db.Ctx);

        var fr = await repo.GetByIdAsync(seeded.Id);
        // Someone else changes user 1 meanwhile; the loaded Sender is now stale.
        await db.Ctx.Database.ExecuteSqlRawAsync("UPDATE Users SET TotalXp = 500 WHERE Id = 1");
        fr!.Status = FriendRequestStatus.Accepted;
        await repo.UpdateAsync(fr);

        db.Ctx.ChangeTracker.Clear();
        Assert.Equal(500, db.Ctx.Users.Single(u => u.Id == 1).TotalXp);
        Assert.Equal(FriendRequestStatus.Accepted, db.Ctx.FriendRequests.Single().Status);
    }
}
