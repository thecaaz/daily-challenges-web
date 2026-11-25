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
    public Task<FriendRequest?> GetByIdAsync(int id) =>
        WithUsers().FirstOrDefaultAsync(fr => fr.Id == id);

    public Task<FriendRequest?> GetExistingAsync(int userId1, int userId2) =>
        WithUsers().FirstOrDefaultAsync(fr =>
            (fr.SenderId == userId1 && fr.ReceiverId == userId2) ||
            (fr.SenderId == userId2 && fr.ReceiverId == userId1));

    public Task<FriendRequest?> GetRelationshipAsync(int requestingUserId, int targetUserId) =>
        GetExistingAsync(requestingUserId, targetUserId);

    public Task<List<FriendRequest>> GetIncomingPendingAsync(int userId) =>
        WithUsers()
            .Where(fr => fr.ReceiverId == userId && fr.Status == FriendRequestStatus.Pending)
            .OrderByDescending(fr => fr.CreatedAt)
            .ToListAsync();

    public Task<List<FriendRequest>> GetOutgoingPendingAsync(int userId) =>
        WithUsers()
            .Where(fr => fr.SenderId == userId && fr.Status == FriendRequestStatus.Pending)
            .OrderByDescending(fr => fr.CreatedAt)
            .ToListAsync();

    public async Task<List<User>> GetFriendsAsync(int userId)
    {
        var friendships = await WithUsers()
            .Where(fr =>
                (fr.SenderId == userId || fr.ReceiverId == userId) &&
                fr.Status == FriendRequestStatus.Accepted)
            .ToListAsync();

        return friendships
            .Select(fr => fr.SenderId == userId ? fr.Receiver! : fr.Sender!)
            .ToList();
    }

    public Task<bool> HasAcceptedFriendAsync(int userId) =>
        db.FriendRequests.AnyAsync(fr =>
            fr.Status == FriendRequestStatus.Accepted &&
            (fr.SenderId == userId || fr.ReceiverId == userId));

    /// <summary>Returns the saved request reloaded with its users.</summary>
    public async Task<FriendRequest> CreateAsync(FriendRequest fr)
    {
        db.FriendRequests.Add(fr);
        await db.SaveInsertsOrDetachAsync([fr]);
        return await GetSavedAsync(fr.Id);
    }

    /// <summary>Marks only the request itself as modified (not its Sender/Receiver graph).</summary>
    public async Task<FriendRequest> UpdateAsync(FriendRequest fr)
    {
        var entry = db.Entry(fr);
        if (entry.State == EntityState.Detached) entry.State = EntityState.Modified;
        await db.SaveChangesAsync();
        entry.State = EntityState.Detached;
        return await GetSavedAsync(fr.Id);
    }

    public async Task<bool> ReopenDeclinedAsync(int id, int senderId, int receiverId, DateTime nowUtc)
    {
        var rows = await db.FriendRequests
            .Where(fr => fr.Id == id && fr.Status == FriendRequestStatus.Declined)
            .ExecuteUpdateAsync(s => s
                .SetProperty(fr => fr.SenderId, senderId)
                .SetProperty(fr => fr.ReceiverId, receiverId)
                .SetProperty(fr => fr.Status, FriendRequestStatus.Pending)
                .SetProperty(fr => fr.CreatedAt, nowUtc)
                .SetProperty(fr => fr.UpdatedAt, (DateTime?)null));
        return rows == 1;
    }

    public async Task DeleteAsync(int id)
    {
        var fr = await db.FriendRequests.FirstOrDefaultAsync(f => f.Id == id);
        if (fr == null) return;

        db.FriendRequests.Remove(fr);
        await db.SaveChangesAsync();
    }

    private IQueryable<FriendRequest> WithUsers() =>
        db.FriendRequests
            .Include(fr => fr.Sender)
            .Include(fr => fr.Receiver)
            .AsNoTracking();

    /// <summary>The row was just saved in this scope, so the reload finds it.</summary>
    private async Task<FriendRequest> GetSavedAsync(int id) => (await GetByIdAsync(id))!;
}
