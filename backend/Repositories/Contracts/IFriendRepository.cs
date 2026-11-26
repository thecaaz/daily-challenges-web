using DailyChallenges.Models;

namespace DailyChallenges.Repositories.Contracts;

public interface IFriendRepository
{
    Task<FriendRequest?> GetByIdAsync(int id, CancellationToken ct);
    /// <summary>The single request row between the two users (either direction, any status), or null.</summary>
    Task<FriendRequest?> GetExistingAsync(int userId1, int userId2, CancellationToken ct);
    Task<FriendRequest?> GetRelationshipAsync(int requestingUserId, int targetUserId, CancellationToken ct);
    Task<List<FriendRequest>> GetIncomingPendingAsync(int userId, CancellationToken ct);
    Task<List<FriendRequest>> GetOutgoingPendingAsync(int userId, CancellationToken ct);
    Task<List<User>> GetFriendsAsync(int userId, CancellationToken ct);
    Task<bool> HasAcceptedFriendAsync(int userId, CancellationToken ct);
    /// <summary>Inserts a request. A second row for the same pair (either direction) fails with a unique-violation DbUpdateException.</summary>
    Task<FriendRequest> CreateAsync(FriendRequest fr, CancellationToken ct);
    /// <summary>Saves the request's own columns (not its Sender/Receiver). The caller sets UpdatedAt.</summary>
    Task<FriendRequest> UpdateAsync(FriendRequest fr, CancellationToken ct);
    /// <summary>
    /// Turns a Declined request back into a new Pending request in the given <paramref name="direction"/>,
    /// created at <paramref name="nowUtc"/>. Returns false when the row is no longer Declined.
    /// </summary>
    Task<bool> ReopenDeclinedAsync(int id, FriendRequestDirection direction, DateTime nowUtc, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

/// <summary>Who asks whom in a friend request.</summary>
public sealed record FriendRequestDirection(int SenderId, int ReceiverId);
