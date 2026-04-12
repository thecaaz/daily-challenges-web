using DailyChallenges.Models;

namespace DailyChallenges.Repositories.Contracts
{
    public interface IFriendRepository
    {
        Task<FriendRequest?> GetByIdAsync(int id);
        Task<FriendRequest?> GetExistingAsync(int userId1, int userId2);
        Task<FriendRequest?> GetRelationshipAsync(int requestingUserId, int targetUserId);
        Task<List<FriendRequest>> GetIncomingPendingAsync(int userId);
        Task<List<FriendRequest>> GetOutgoingPendingAsync(int userId);
        Task<List<User>> GetFriendsAsync(int userId);
        Task<FriendRequest> CreateAsync(FriendRequest fr);
        Task<FriendRequest> UpdateAsync(FriendRequest fr);
        Task DeleteAsync(int id);
    }
}
