using DailyChallenges.Data;
using DailyChallenges.IntegrationTests.Infrastructure;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.IntegrationTests;

public class EfFriendRepositoryTests
{
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
        db.Ctx.SeedUser(1);
        db.Ctx.SeedUser(2);
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
        db.Ctx.SeedUser(1);
        db.Ctx.SeedUser(2);
        SeedFriendRequest(db.Ctx, 1, 2);
        var repo = new EfFriendRepository(db.Ctx);
        const int receiverOfTheRequest = 2, senderOfTheRequest = 1;

        var result = await repo.GetExistingAsync(receiverOfTheRequest, senderOfTheRequest);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetExisting_ReturnsNull_WhenNone()
    {
        await using var db = new TestDb();
        db.Ctx.SeedUser(1);
        db.Ctx.SeedUser(2);
        var repo = new EfFriendRepository(db.Ctx);

        var result = await repo.GetExistingAsync(1, 2);

        Assert.Null(result);
    }

    // ── GetIncomingPendingAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetIncomingPending_OnlyReturnsReceiversPendingRequests()
    {
        await using var db = new TestDb();
        db.Ctx.SeedUser(1);
        db.Ctx.SeedUser(2);
        db.Ctx.SeedUser(3);
        db.Ctx.SeedUser(4);
        var incomingFromUser1 = SeedFriendRequest(db.Ctx, 1, 2);
        var incomingFromUser3 = SeedFriendRequest(db.Ctx, 3, 2);
        var outgoingToUser4 = SeedFriendRequest(db.Ctx, 2, 4);
        var repo = new EfFriendRepository(db.Ctx);

        var results = await repo.GetIncomingPendingAsync(2);

        Assert.Equal(new[] { incomingFromUser1.Id, incomingFromUser3.Id }, results.Select(r => r.Id).OrderBy(id => id));
        Assert.DoesNotContain(results, r => r.Id == outgoingToUser4.Id);
        Assert.All(results, r => Assert.Equal(2, r.ReceiverId));
    }

    [Fact]
    public async Task GetIncomingPending_ExcludesAccepted()
    {
        await using var db = new TestDb();
        db.Ctx.SeedUser(1);
        db.Ctx.SeedUser(2);
        SeedFriendRequest(db.Ctx, 1, 2, FriendRequestStatus.Accepted);
        var repo = new EfFriendRepository(db.Ctx);

        var results = await repo.GetIncomingPendingAsync(2);

        Assert.Empty(results);
    }

    // ── GetOutgoingPendingAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetOutgoingPending_OnlyReturnsSendersPendingRequests_NotIncomingOrDeclined()
    {
        await using var db = new TestDb();
        db.Ctx.SeedUser(1);
        db.Ctx.SeedUser(2);
        db.Ctx.SeedUser(3);
        db.Ctx.SeedUser(4);
        db.Ctx.SeedUser(5);
        var outgoingToUser2 = SeedFriendRequest(db.Ctx, 1, 2);
        var outgoingToUser3 = SeedFriendRequest(db.Ctx, 1, 3);
        SeedFriendRequest(db.Ctx, 4, 1);
        SeedFriendRequest(db.Ctx, 1, 5, FriendRequestStatus.Declined);
        var repo = new EfFriendRepository(db.Ctx);

        var results = await repo.GetOutgoingPendingAsync(1);

        Assert.Equal(new[] { outgoingToUser2.Id, outgoingToUser3.Id }, results.Select(r => r.Id).OrderBy(id => id));
        Assert.All(results, r => Assert.Equal(1, r.SenderId));
    }

    // ── GetFriendsAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetFriends_ReturnsFriendUsers_BothDirections()
    {
        await using var db = new TestDb();
        db.Ctx.SeedUser(1);
        db.Ctx.SeedUser(2);
        db.Ctx.SeedUser(3);
        SeedFriendRequest(db.Ctx, senderId: 1, receiverId: 2, FriendRequestStatus.Accepted);
        SeedFriendRequest(db.Ctx, senderId: 3, receiverId: 1, FriendRequestStatus.Accepted);
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
        db.Ctx.SeedUser(1);
        db.Ctx.SeedUser(2);
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
        db.Ctx.SeedUser(1);
        db.Ctx.SeedUser(2);
        SeedFriendRequest(db.Ctx, 1, 2, FriendRequestStatus.Accepted);
        var repo = new EfFriendRepository(db.Ctx);

        Assert.True(await repo.HasAcceptedFriendAsync(1));
    }

    [Fact]
    public async Task HasAcceptedFriend_True_WhenUserIsReceiver()
    {
        await using var db = new TestDb();
        db.Ctx.SeedUser(1);
        db.Ctx.SeedUser(2);
        SeedFriendRequest(db.Ctx, senderId: 2, receiverId: 1, FriendRequestStatus.Accepted);
        var repo = new EfFriendRepository(db.Ctx);

        Assert.True(await repo.HasAcceptedFriendAsync(1));
    }

    [Fact]
    public async Task HasAcceptedFriend_False_WhenOnlyPending()
    {
        await using var db = new TestDb();
        db.Ctx.SeedUser(1);
        db.Ctx.SeedUser(2);
        SeedFriendRequest(db.Ctx, 1, 2, FriendRequestStatus.Pending);
        var repo = new EfFriendRepository(db.Ctx);

        Assert.False(await repo.HasAcceptedFriendAsync(1));
    }

    [Fact]
    public async Task HasAcceptedFriend_False_WhenNoRequests()
    {
        await using var db = new TestDb();
        db.Ctx.SeedUser(1);
        var repo = new EfFriendRepository(db.Ctx);

        Assert.False(await repo.HasAcceptedFriendAsync(1));
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_RemovesFriendRequest()
    {
        await using var db = new TestDb();
        db.Ctx.SeedUser(1);
        db.Ctx.SeedUser(2);
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

        const int missingRequestId = 9999;

        await repo.DeleteAsync(missingRequestId);
    }

    // ── One row per pair; Declined ────────────────────────────────────────────

    [Fact]
    public async Task Reverse_direction_insert_is_rejected_by_the_pair_index_and_detached()
    {
        await using var db = new TestDb();
        db.Ctx.SeedUser(1);
        db.Ctx.SeedUser(2);
        SeedFriendRequest(db.Ctx, 1, 2);
        var repo = new EfFriendRepository(db.Ctx);

        var ex = await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() =>
            repo.CreateAsync(new FriendRequest { SenderId = 2, ReceiverId = 1, Status = FriendRequestStatus.Pending }));
        Assert.True(ex.IsUniqueViolation());

        db.Ctx.SeedUser(3);
        Assert.Equal(1, db.Ctx.FriendRequests.Count());
    }

    [Fact]
    public async Task Declined_requests_are_not_friends_or_pending()
    {
        await using var db = new TestDb();
        db.Ctx.SeedUser(1);
        db.Ctx.SeedUser(2);
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
        db.Ctx.SeedUser(1);
        db.Ctx.SeedUser(2);
        var fr = SeedFriendRequest(db.Ctx, 1, 2, FriendRequestStatus.Declined);
        var repo = new EfFriendRepository(db.Ctx);
        var now = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        var reopenedOnce = await repo.ReopenDeclinedAsync(fr.Id, new FriendRequestDirection(2, 1), now);
        var reopenedWhenNoLongerDeclined = await repo.ReopenDeclinedAsync(fr.Id, new FriendRequestDirection(2, 1), now);

        Assert.True(reopenedOnce);
        Assert.False(reopenedWhenNoLongerDeclined);

        var reopened = await repo.GetByIdAsync(fr.Id);
        Assert.Equal((2, 1, FriendRequestStatus.Pending, now), (reopened!.SenderId, reopened.ReceiverId, reopened.Status, reopened.CreatedAt));
        Assert.Null(reopened.UpdatedAt);
    }

    /// <summary>Someone else changes user 1 while the request is loaded, so the loaded Sender is stale.</summary>
    private static Task ChangeSenderXpBehindTheContextAsync(TestDb db, int totalXp) =>
        db.Ctx.Database.ExecuteSqlRawAsync("UPDATE Users SET TotalXp = {0} WHERE Id = 1", totalXp);

    [Fact]
    public async Task Update_does_not_write_back_the_loaded_users()
    {
        await using var db = new TestDb();
        db.Ctx.SeedUser(1);
        db.Ctx.SeedUser(2);
        var seeded = SeedFriendRequest(db.Ctx, 1, 2);
        db.Ctx.ChangeTracker.Clear();
        var repo = new EfFriendRepository(db.Ctx);

        var fr = await repo.GetByIdAsync(seeded.Id);
        await ChangeSenderXpBehindTheContextAsync(db, 500);
        fr!.Status = FriendRequestStatus.Accepted;
        await repo.UpdateAsync(fr);

        db.Ctx.ChangeTracker.Clear();
        Assert.Equal(500, db.Ctx.Users.Single(u => u.Id == 1).TotalXp);
        Assert.Equal(FriendRequestStatus.Accepted, db.Ctx.FriendRequests.Single().Status);
    }
}
