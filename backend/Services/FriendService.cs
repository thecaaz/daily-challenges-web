using DailyChallenges.Data;
using DailyChallenges.Exceptions;
using DailyChallenges.Achievements;
using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Services
{
    public class FriendService : IFriendService
    {
        /// <summary>How long after a decline the declined sender cannot send the same user a new request.</summary>
        public static readonly TimeSpan RerequestCooldown = TimeSpan.FromHours(24);

        /// <summary>No second friend_request notification from the same sender while an unread one is younger than this.</summary>
        public static readonly TimeSpan NotificationCooldown = TimeSpan.FromMinutes(15);

        private readonly IFriendRepository _friends;
        private readonly IUserProfileRepository _users;
        private readonly INotificationRepository _notifications;
        private readonly LevelCalculator _levelCalc;
        private readonly IAchievementService _achievements;
        private readonly TimeProvider _time;

        public FriendService(
            IFriendRepository friends,
            IUserProfileRepository users,
            INotificationRepository notifications,
            LevelCalculator levelCalc,
            IAchievementService achievements,
            TimeProvider? time = null)
        {
            _time = time ?? TimeProvider.System;
            _friends = friends;
            _users = users;
            _notifications = notifications;
            _levelCalc = levelCalc;
            _achievements = achievements;
        }

        public async Task<FriendRequestDto> SendRequestAsync(int senderId, int targetUserId)
        {
            if (senderId == targetUserId)
                throw new ArgumentException("You cannot send a friend request to yourself.");

            var target = await _users.GetByIdAsync(targetUserId)
                ?? throw new KeyNotFoundException("User not found.");

            var now = _time.GetUtcNow().UtcDateTime;
            FriendRequest fr;

            // At most one row per pair of users (either direction, enforced by a unique index).
            var existing = await _friends.GetExistingAsync(senderId, targetUserId);
            if (existing == null)
            {
                try
                {
                    fr = await _friends.CreateAsync(new FriendRequest
                    {
                        SenderId = senderId,
                        ReceiverId = targetUserId,
                        Status = FriendRequestStatus.Pending,
                        CreatedAt = now
                    });
                }
                catch (DbUpdateException ex) when (ex.IsUniqueViolation())
                {
                    // A concurrent request for the same pair (double click, or both users at once) won.
                    throw new ConflictException("A friend request already exists between these users.");
                }
            }
            else if (existing.Status == FriendRequestStatus.Accepted)
            {
                throw new ConflictException("You are already friends.");
            }
            else if (existing.Status == FriendRequestStatus.Pending)
            {
                throw new ConflictException("A friend request already exists between these users.");
            }
            else
            {
                // Declined: the declined sender waits out the cooldown; the user who declined may ask right away.
                var declinedAt = existing.UpdatedAt ?? existing.CreatedAt;
                if (existing.SenderId == senderId && declinedAt > now - RerequestCooldown)
                    throw new ConflictException($"{target.Username} declined your friend request recently. Try again after 24 hours.");

                if (!await _friends.ReopenDeclinedAsync(existing.Id, senderId, targetUserId, now))
                    throw new ConflictException("A friend request already exists between these users.");
                fr = (await _friends.GetByIdAsync(existing.Id))
                    ?? throw new ConflictException("A friend request already exists between these users.");
            }

            var sender = await _users.GetByIdAsync(senderId)
                ?? throw new KeyNotFoundException("Sender not found.");

            // Prevent notification spam (e.g. request, cancel, request again): skip the notification while
            // an unread friend_request from this exact sender is younger than the cooldown.
            var recentSimilar = await _notifications.HasRecentUnreadFromActorAsync(
                targetUserId, "friend_request", senderId, now - NotificationCooldown);

            if (!recentSimilar)
            {
                await _notifications.CreateBatchAsync(new List<Notification>
                {
                    new Notification
                    {
                        UserId = targetUserId,
                        GameId = null,
                        ScoringDay = now.Date,
                        Message = $"{sender.Username} sent you a friend request.",
                        Type = "friend_request",
                        ActorUserId = senderId,
                        CreatedAt = now
                    }
                });
            }

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

            // Wrong owner reads as "not found" so callers cannot probe which request ids exist.
            if (fr.ReceiverId != receiverId)
                throw new NotFoundException("Friend request not found.");

            if (fr.Status != FriendRequestStatus.Pending)
                throw new ConflictException("This request is no longer pending.");

            var now = _time.GetUtcNow().UtcDateTime;
            if (!accept)
            {
                // Keep the row as Declined: UpdatedAt starts the sender's re-request cooldown.
                fr.Status = FriendRequestStatus.Declined;
                fr.UpdatedAt = now;
                await _friends.UpdateAsync(fr);
                return;
            }

            fr.Status = FriendRequestStatus.Accepted;
            fr.UpdatedAt = now;
            await _friends.UpdateAsync(fr);

            var receiver = await _users.GetByIdAsync(receiverId);
            await _notifications.CreateBatchAsync(new List<Notification>
            {
                new Notification
                {
                    UserId = fr.SenderId,
                    GameId = null,
                    ScoringDay = now.Date,
                    Message = $"{receiver?.Username ?? "Someone"} accepted your friend request.",
                    Type = "friend_request_accepted",
                    ActorUserId = receiverId,
                    CreatedAt = now
                }
            });

            // Check first_friend achievement for both parties.
            await _achievements.CheckAndAwardAsync(fr.SenderId, AchievementTrigger.FriendAccepted);
            await _achievements.CheckAndAwardAsync(receiverId, AchievementTrigger.FriendAccepted);
        }

        public async Task CancelRequestAsync(int requestId, int senderId)
        {
            var fr = await _friends.GetByIdAsync(requestId)
                ?? throw new KeyNotFoundException("Friend request not found.");

            // Same as above: hide requests the caller did not send.
            if (fr.SenderId != senderId)
                throw new NotFoundException("Friend request not found.");

            if (fr.Status != FriendRequestStatus.Pending)
                throw new ConflictException("Only pending requests can be cancelled.");

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
            return users.Select(u => DtoMapper.ToFriendDto(u, _levelCalc, _time.GetUtcNow().UtcDateTime)).ToList();
        }

        public async Task<string> GetRelationshipStatusAsync(int requestingUserId, int targetUserId)
        {
            var fr = await _friends.GetRelationshipAsync(requestingUserId, targetUserId);
            // A declined request reads as no relationship.
            if (fr == null || fr.Status == FriendRequestStatus.Declined) return "none";

            if (fr.Status == FriendRequestStatus.Accepted) return "friends";

            // Pending — determine direction
            return fr.SenderId == requestingUserId ? "pending_sent" : "pending_received";
        }

        public async Task RemoveFriendAsync(int userId, int friendId)
        {
            var fr = await _friends.GetExistingAsync(userId, friendId)
                ?? throw new KeyNotFoundException("Friendship not found.");

            if (fr.Status != FriendRequestStatus.Accepted)
                throw new NotFoundException("Friendship not found.");

            await _friends.DeleteAsync(fr.Id);
        }
    }
}
