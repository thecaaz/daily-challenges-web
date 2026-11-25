using DailyChallenges.DTOs;

namespace DailyChallenges.Services.Contracts;

/// <summary>Sending, answering and cancelling friend requests.</summary>
public interface IFriendRequestService
{
    /// <summary>
    /// Creates a pending request, or reopens a declined one. Throws <see cref="ArgumentException"/> for
    /// a request to oneself, <see cref="KeyNotFoundException"/> for an unknown user and
    /// <see cref="Exceptions.ConflictException"/> when the pair is already friends, already has a pending
    /// request, or the sender was declined within the re-request cooldown.
    /// </summary>
    Task<FriendRequestDto> SendRequestAsync(int senderId, int targetUserId);

    /// <summary>Same as <see cref="SendRequestAsync"/>, addressed by (trimmed) username.</summary>
    Task<FriendRequestDto> SendRequestByUsernameAsync(int senderId, string username);

    /// <summary>Accepts a pending request addressed to <paramref name="receiverId"/> and notifies its sender.</summary>
    Task AcceptRequestAsync(int requestId, int receiverId);

    /// <summary>Declines a pending request addressed to <paramref name="receiverId"/>; the row is kept as Declined.</summary>
    Task DeclineRequestAsync(int requestId, int receiverId);

    /// <summary>Deletes a pending request the caller sent.</summary>
    Task CancelRequestAsync(int requestId, int senderId);
}
