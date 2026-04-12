using DailyChallenges.DTOs;

namespace DailyChallenges.Services.Contracts
{
    public interface IFriendService
    {
        Task<FriendRequestDto> SendRequestAsync(int senderId, int targetUserId);
        Task<FriendRequestDto> SendRequestByUsernameAsync(int senderId, string username);
        Task RespondToRequestAsync(int requestId, int receiverId, bool accept);
        Task CancelRequestAsync(int requestId, int senderId);
        Task<List<FriendRequestDto>> GetIncomingRequestsAsync(int userId);
        Task<List<FriendRequestDto>> GetOutgoingRequestsAsync(int userId);
        Task<List<FriendDto>> GetFriendsAsync(int userId);

        /// <summary>Returns "none", "pending_sent", "pending_received", or "friends".</summary>
        Task<string> GetRelationshipStatusAsync(int requestingUserId, int targetUserId);

        Task RemoveFriendAsync(int userId, int friendId);
    }
}
