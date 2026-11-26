using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

/// <summary>
/// Reads are untracked and include both users: <see cref="UpdateAsync"/> attaches only the request itself,
/// so the loaded Sender/Receiver users are never written back.
/// </summary>
public sealed class EfFriendRepository(AppDbContext db) : IFriendRepository
{
    public Task<FriendRequest?> GetByIdAsync(int id, CancellationToken ct) =>
        WithUsers().FirstOrDefaultAsync(fr => fr.Id == id, ct);

    public Task<FriendRequest?> GetExistingAsync(int userId1, int userId2, CancellationToken ct) =>
        WithUsers().FirstOrDefaultAsync(fr =>
            (fr.SenderId == userId1 && fr.ReceiverId == userId2) ||
            (fr.SenderId == userId2 && fr.ReceiverId == userId1), ct);

    public Task<FriendRequest?> GetRelationshipAsync(int requestingUserId, int targetUserId, CancellationToken ct) =>
        GetExistingAsync(requestingUserId, targetUserId, ct);

    public Task<List<FriendRequest>> GetIncomingPendingAsync(int userId, CancellationToken ct) =>
        WithUsers()
            .Where(fr => fr.ReceiverId == userId && fr.Status == FriendRequestStatus.Pending)
            .OrderByDescending(fr => fr.CreatedAt)
            .ToListAsync(ct);

    public Task<List<FriendRequest>> GetOutgoingPendingAsync(int userId, CancellationToken ct) =>
        WithUsers()
            .Where(fr => fr.SenderId == userId && fr.Status == FriendRequestStatus.Pending)
            .OrderByDescending(fr => fr.CreatedAt)
            .ToListAsync(ct);

    public async Task<List<User>> GetFriendsAsync(int userId, CancellationToken ct)
    {
        var friendships = await WithUsers()
            .Where(fr =>
                (fr.SenderId == userId || fr.ReceiverId == userId) &&
                fr.Status == FriendRequestStatus.Accepted)
            .ToListAsync(ct);

        return friendships
            .Select(fr => fr.SenderId == userId ? fr.Receiver! : fr.Sender!)
            .ToList();
    }

    public Task<bool> HasAcceptedFriendAsync(int userId, CancellationToken ct) =>
        db.FriendRequests.AnyAsync(fr =>
            fr.Status == FriendRequestStatus.Accepted &&
            (fr.SenderId == userId || fr.ReceiverId == userId), ct);

    /// <summary>Returns the saved request reloaded with its users.</summary>
    public async Task<FriendRequest> CreateAsync(FriendRequest fr, CancellationToken ct)
    {
        db.FriendRequests.Add(fr);
        await db.SaveInsertsOrDetachAsync([fr], ct);
        return await GetSavedAsync(fr.Id, ct);
    }

    /// <summary>Marks only the request itself as modified (not its Sender/Receiver graph).</summary>
    public async Task<FriendRequest> UpdateAsync(FriendRequest fr, CancellationToken ct)
    {
        var entry = db.Entry(fr);
        if (entry.State == EntityState.Detached) entry.State = EntityState.Modified;
        await db.SaveChangesAsync(ct);
        entry.State = EntityState.Detached;
        return await GetSavedAsync(fr.Id, ct);
    }

    public async Task<bool> ReopenDeclinedAsync(int id, FriendRequestDirection direction, DateTime nowUtc, CancellationToken ct)
    {
        var rows = await db.FriendRequests
            .Where(fr => fr.Id == id && fr.Status == FriendRequestStatus.Declined)
            .ExecuteUpdateAsync(s => s
                .SetProperty(fr => fr.SenderId, direction.SenderId)
                .SetProperty(fr => fr.ReceiverId, direction.ReceiverId)
                .SetProperty(fr => fr.Status, FriendRequestStatus.Pending)
                .SetProperty(fr => fr.CreatedAt, nowUtc)
                .SetProperty(fr => fr.UpdatedAt, (DateTime?)null), ct);
        return rows == 1;
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var fr = await db.FriendRequests.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (fr == null) return;

        db.FriendRequests.Remove(fr);
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<FriendRequest> WithUsers() =>
        db.FriendRequests
            .Include(fr => fr.Sender)
            .Include(fr => fr.Receiver)
            .AsNoTracking();

    /// <summary>The row was just saved in this scope, so the reload finds it.</summary>
    private async Task<FriendRequest> GetSavedAsync(int id, CancellationToken ct) => (await GetByIdAsync(id, ct))!;
}
