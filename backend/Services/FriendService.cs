using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Services
{
    public class FriendService : IFriendService
    {
        private readonly IFriendRepository _friends;
        private readonly IUserProfileRepository _users;
        private readonly INotificationRepository _notifications;
        private readonly LevelCalculator _levelCalc;

        public FriendService(
            IFriendRepository friends,
            IUserProfileRepository users,
            INotificationRepository notifications,
            LevelCalculator levelCalc)
        {
            _friends = friends;
            _users = users;
            _notifications = notifications;
            _levelCalc = levelCalc;
        }

        public async Task<FriendRequestDto> SendRequestAsync(int senderId, int targetUserId)
        {
            if (senderId == targetUserId)
                throw new ArgumentException("You cannot send a friend request to yourself.");

            var _ = await _users.GetByIdAsync(targetUserId)
                ?? throw new KeyNotFoundException("User not found.");

            var existing = await _friends.GetExistingAsync(senderId, targetUserId);
            if (existing != null)
            {
                if (existing.Status == FriendRequestStatus.Accepted)
                    throw new InvalidOperationException("You are already friends.");
                throw new InvalidOperationException("A friend request already exists between these users.");
            }

            var sender = await _users.GetByIdAsync(senderId)
                ?? throw new KeyNotFoundException("Sender not found.");

            var fr = await _friends.CreateAsync(new FriendRequest
            {
                SenderId = senderId,
                ReceiverId = targetUserId,
                Status = FriendRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });

            await _notifications.CreateBatchAsync(
            [
                new Notification
                {
                    UserId = targetUserId,
                    GameId = null,
                    ScoringDay = DateTime.UtcNow.Date,
                    Message = $"{sender.Username} sent you a friend request.",
                    Type = "friend_request"
                }
            ]);

            return DtoMapper.ToDto(fr);
        }

        public async Task<FriendRequestDto> SendRequestByUsernameAsync(int senderId, string username)
        {
            var target = await _users.GetByUsernameAsync(username.Trim())
                ?? throw new KeyNotFoundException($"No user found with username '{username}'.");

            return await SendRequestAsync(senderId, target.Id);
        }

        public async Task RespondToRequestAsync(int requestId, int receiverId, bool accept)
        {
            var fr = await _friends.GetByIdAsync(requestId)
                ?? throw new KeyNotFoundException("Friend request not found.");

            if (fr.ReceiverId != receiverId)
                throw new InvalidOperationException("Not authorised to respond to this request.");

            if (fr.Status != FriendRequestStatus.Pending)
                throw new InvalidOperationException("This request is no longer pending.");

            if (!accept)
            {
                await _friends.DeleteAsync(fr.Id);
                return;
            }

            fr.Status = FriendRequestStatus.Accepted;
            await _friends.UpdateAsync(fr);

            var receiver = await _users.GetByIdAsync(receiverId);
            await _notifications.CreateBatchAsync(
            [
                new Notification
                {
                    UserId = fr.SenderId,
                    GameId = null,
                    ScoringDay = DateTime.UtcNow.Date,
                    Message = $"{receiver?.Username ?? "Someone"} accepted your friend request.",
                    Type = "friend_request_accepted"
                }
            ]);
        }

        public async Task CancelRequestAsync(int requestId, int senderId)
        {
            var fr = await _friends.GetByIdAsync(requestId)
                ?? throw new KeyNotFoundException("Friend request not found.");

            if (fr.SenderId != senderId)
                throw new InvalidOperationException("Not authorised to cancel this request.");

            if (fr.Status != FriendRequestStatus.Pending)
                throw new InvalidOperationException("Only pending requests can be cancelled.");

            await _friends.DeleteAsync(fr.Id);
        }

        public async Task<List<FriendRequestDto>> GetIncomingRequestsAsync(int userId)
        {
            var items = await _friends.GetIncomingPendingAsync(userId);
            return items.Select(DtoMapper.ToDto).ToList();
        }

        public async Task<List<FriendRequestDto>> GetOutgoingRequestsAsync(int userId)
        {
            var items = await _friends.GetOutgoingPendingAsync(userId);
            return items.Select(DtoMapper.ToDto).ToList();
        }

        public async Task<List<FriendDto>> GetFriendsAsync(int userId)
        {
            var users = await _friends.GetFriendsAsync(userId);
            return users.Select(u => DtoMapper.ToFriendDto(u, _levelCalc)).ToList();
        }

        public async Task<string> GetRelationshipStatusAsync(int requestingUserId, int targetUserId)
        {
            var fr = await _friends.GetRelationshipAsync(requestingUserId, targetUserId);
            if (fr == null) return "none";

            if (fr.Status == FriendRequestStatus.Accepted) return "friends";

            // Pending — determine direction
            return fr.SenderId == requestingUserId ? "pending_sent" : "pending_received";
        }

        public async Task RemoveFriendAsync(int userId, int friendId)
        {
            var fr = await _friends.GetExistingAsync(userId, friendId)
                ?? throw new KeyNotFoundException("Friendship not found.");

            if (fr.Status != FriendRequestStatus.Accepted)
                throw new InvalidOperationException("These users are not friends.");

            await _friends.DeleteAsync(fr.Id);
        }
    }
}
