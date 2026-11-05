using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories
{
    public class EfFriendRepository : IFriendRepository
    {
        private readonly AppDbContext _db;
        public EfFriendRepository(AppDbContext db) => _db = db;

        // Reads are untracked: UpdateAsync attaches only the request itself, so the loaded Sender/Receiver
        // users are never written back.
        public async Task<FriendRequest?> GetByIdAsync(int id)
        {
            return await _db.FriendRequests
                .Include(fr => fr.Sender)
                .Include(fr => fr.Receiver)
                .AsNoTracking()
                .FirstOrDefaultAsync(fr => fr.Id == id);
        }

        public async Task<FriendRequest?> GetExistingAsync(int userId1, int userId2)
        {
            return await _db.FriendRequests
                .Include(fr => fr.Sender)
                .Include(fr => fr.Receiver)
                .AsNoTracking()
                .FirstOrDefaultAsync(fr =>
                    (fr.SenderId == userId1 && fr.ReceiverId == userId2) ||
                    (fr.SenderId == userId2 && fr.ReceiverId == userId1));
        }

        public async Task<FriendRequest?> GetRelationshipAsync(int requestingUserId, int targetUserId)
        {
            return await GetExistingAsync(requestingUserId, targetUserId);
        }

        public async Task<List<FriendRequest>> GetIncomingPendingAsync(int userId)
        {
            return await _db.FriendRequests
                .Include(fr => fr.Sender)
                .Include(fr => fr.Receiver)
                .Where(fr => fr.ReceiverId == userId && fr.Status == FriendRequestStatus.Pending)
                .OrderByDescending(fr => fr.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<FriendRequest>> GetOutgoingPendingAsync(int userId)
        {
            return await _db.FriendRequests
                .Include(fr => fr.Sender)
                .Include(fr => fr.Receiver)
                .Where(fr => fr.SenderId == userId && fr.Status == FriendRequestStatus.Pending)
                .OrderByDescending(fr => fr.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<User>> GetFriendsAsync(int userId)
        {
            var friendships = await _db.FriendRequests
                .Include(fr => fr.Sender)
                .Include(fr => fr.Receiver)
                .Where(fr =>
                    (fr.SenderId == userId || fr.ReceiverId == userId) &&
                    fr.Status == FriendRequestStatus.Accepted)
                .AsNoTracking()
                .ToListAsync();

            return friendships
                .Select(fr => fr.SenderId == userId ? fr.Receiver! : fr.Sender!)
                .ToList();
        }

        public async Task<bool> HasAcceptedFriendAsync(int userId)
        {
            return await _db.FriendRequests.AnyAsync(fr =>
                fr.Status == FriendRequestStatus.Accepted &&
                (fr.SenderId == userId || fr.ReceiverId == userId));
        }

        public async Task<FriendRequest> CreateAsync(FriendRequest fr)
        {
            _db.FriendRequests.Add(fr);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch
            {
                // Detach the failed insert, or every later SaveChanges in this scope retries (and fails on) it.
                _db.Entry(fr).State = EntityState.Detached;
                throw;
            }
            // Reload with nav props
            return (await GetByIdAsync(fr.Id))!;
        }

        public async Task<FriendRequest> UpdateAsync(FriendRequest fr)
        {
            // Mark only the request itself as modified (not its Sender/Receiver graph).
            var entry = _db.Entry(fr);
            if (entry.State == EntityState.Detached) entry.State = EntityState.Modified;
            await _db.SaveChangesAsync();
            entry.State = EntityState.Detached;
            return (await GetByIdAsync(fr.Id))!;
        }

        public async Task<bool> ReopenDeclinedAsync(int id, int senderId, int receiverId, DateTime nowUtc)
        {
            var rows = await _db.FriendRequests
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
            var fr = await _db.FriendRequests.FirstOrDefaultAsync(f => f.Id == id);
            if (fr == null) return;
            _db.FriendRequests.Remove(fr);
            await _db.SaveChangesAsync();
        }
    }
}
