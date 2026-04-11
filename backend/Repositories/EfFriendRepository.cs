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

        public async Task<FriendRequest?> GetByIdAsync(int id)
        {
            return await _db.FriendRequests
                .Include(fr => fr.Sender)
                .Include(fr => fr.Receiver)
                .FirstOrDefaultAsync(fr => fr.Id == id);
        }

        public async Task<FriendRequest?> GetExistingAsync(int userId1, int userId2)
        {
            return await _db.FriendRequests
                .Include(fr => fr.Sender)
                .Include(fr => fr.Receiver)
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

        public async Task<FriendRequest> CreateAsync(FriendRequest fr)
        {
            _db.FriendRequests.Add(fr);
            await _db.SaveChangesAsync();
            // Reload with nav props
            return (await GetByIdAsync(fr.Id))!;
        }

        public async Task<FriendRequest> UpdateAsync(FriendRequest fr)
        {
            fr.UpdatedAt = DateTime.UtcNow;
            _db.FriendRequests.Update(fr);
            await _db.SaveChangesAsync();
            return (await GetByIdAsync(fr.Id))!;
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
