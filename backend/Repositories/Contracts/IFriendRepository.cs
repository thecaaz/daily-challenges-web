using DailyChallenges.Models;

namespace DailyChallenges.Repositories.Contracts
{
    public interface IFriendRepository
    {
        Task<FriendRequest?> GetByIdAsync(int id);
        /// <summary>The single request row between the two users (either direction, any status), or null.</summary>
        Task<FriendRequest?> GetExistingAsync(int userId1, int userId2);
        Task<FriendRequest?> GetRelationshipAsync(int requestingUserId, int targetUserId);
        Task<List<FriendRequest>> GetIncomingPendingAsync(int userId);
        Task<List<FriendRequest>> GetOutgoingPendingAsync(int userId);
        Task<List<User>> GetFriendsAsync(int userId);
        Task<bool> HasAcceptedFriendAsync(int userId);
        /// <summary>Inserts a request. A second row for the same pair (either direction) fails with a unique-violation DbUpdateException.</summary>
        Task<FriendRequest> CreateAsync(FriendRequest fr);
        /// <summary>Saves the request's own columns (not its Sender/Receiver). The caller sets UpdatedAt.</summary>
        Task<FriendRequest> UpdateAsync(FriendRequest fr);
        /// <summary>
        /// Turns a Declined request back into a new Pending request from <paramref name="senderId"/> to
        /// <paramref name="receiverId"/>. Returns false when the row is no longer Declined.
        /// </summary>
        Task<bool> ReopenDeclinedAsync(int id, int senderId, int receiverId, DateTime nowUtc);
        Task DeleteAsync(int id);
    }
}
