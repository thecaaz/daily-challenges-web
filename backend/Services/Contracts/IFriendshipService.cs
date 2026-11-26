using DailyChallenges.DTOs;

namespace DailyChallenges.Services.Contracts;

/// <summary>Reading friendships and pending requests, and ending a friendship.</summary>
public interface IFriendshipService
{
    Task<List<FriendRequestDto>> GetIncomingRequestsAsync(int userId);
    Task<List<FriendRequestDto>> GetOutgoingRequestsAsync(int userId);
    Task<List<FriendDto>> GetFriendsAsync(int userId);

    /// <summary>Returns one of the <see cref="RelationshipStatus"/> values.</summary>
    Task<string> GetRelationshipStatusAsync(int requestingUserId, int targetUserId);

    /// <summary>Throws <see cref="Exceptions.NotFoundException"/> when the two are not friends.</summary>
    Task RemoveFriendAsync(int userId, int friendId);
}

/// <summary>Values of the relationship status the friends API reports.</summary>
public static class RelationshipStatus
{
    public const string None = "none";
    public const string PendingSent = "pending_sent";
    public const string PendingReceived = "pending_received";
    public const string Friends = "friends";
}
