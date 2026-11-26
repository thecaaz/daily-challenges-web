using DailyChallenges.Achievements;
using DailyChallenges.Data;
using DailyChallenges.DTOs;
using DailyChallenges.Exceptions;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Services;

public sealed class FriendRequestService(
    IFriendRepository friends,
    IUserProfileRepository users,
    INotificationRepository notifications,
    IAchievementService achievements,
    TimeProvider time) : IFriendRequestService
{
    /// <summary>How long after a decline the declined sender cannot send the same user a new request.</summary>
    public static readonly TimeSpan RerequestCooldown = TimeSpan.FromHours(24);

    /// <summary>No second friend_request notification from the same sender while an unread one is younger than this.</summary>
    public static readonly TimeSpan NotificationCooldown = TimeSpan.FromMinutes(15);

    private const string AlreadyRequestedMessage = "A friend request already exists between these users.";
    private const string RequestNotFoundMessage = "Friend request not found.";

    /// <summary>
    /// Once the request is saved, the sender lookup and the notification run with
    /// <see cref="CancellationToken.None"/>, so a client abort cannot leave the saved request unannounced.
    /// </summary>
    public async Task<FriendRequestDto> SendRequestAsync(int senderId, int targetUserId, CancellationToken ct)
    {
        if (senderId == targetUserId)
            throw new ValidationException("You cannot send a friend request to yourself.");

        var target = await users.GetByIdAsync(targetUserId, ct)
            ?? throw new NotFoundException("User not found.");
        var attempt = new FriendRequestAttempt(senderId, target, time.GetUtcNow().UtcDateTime);

        var request = await CreateOrReopenAsync(attempt, ct);
        var sender = await users.GetByIdAsync(senderId, CancellationToken.None)
            ?? throw new NotFoundException("Sender not found.");
        await NotifyTargetUnlessRecentlyNotifiedAsync(attempt, sender.Username);
        return DtoMapper.ToDto(request);
    }

    public async Task<FriendRequestDto> SendRequestByUsernameAsync(int senderId, string username, CancellationToken ct)
    {
        var target = await users.GetByUsernameAsync(username.Trim(), ct)
            ?? throw new NotFoundException($"No user found with username '{username}'.");

        return await SendRequestAsync(senderId, target.Id, ct);
    }

    /// <summary>The notification and achievement checks run after the status is saved, without the request's token.</summary>
    public async Task AcceptRequestAsync(int requestId, int receiverId, CancellationToken ct)
    {
        var request = await GetPendingRequestToAsync(requestId, receiverId, ct);
        var now = time.GetUtcNow().UtcDateTime;

        await SaveStatusAsync(request, FriendRequestStatus.Accepted, now, ct);
        await NotifySenderOfAcceptanceAsync(request, now);
        await achievements.CheckAndAwardAsync(request.SenderId, AchievementTrigger.FriendAccepted);
        await achievements.CheckAndAwardAsync(receiverId, AchievementTrigger.FriendAccepted);
    }

    /// <summary>The row is kept as Declined: its UpdatedAt starts the sender's <see cref="RerequestCooldown"/>.</summary>
    public async Task DeclineRequestAsync(int requestId, int receiverId, CancellationToken ct)
    {
        var request = await GetPendingRequestToAsync(requestId, receiverId, ct);
        await SaveStatusAsync(request, FriendRequestStatus.Declined, time.GetUtcNow().UtcDateTime, ct);
    }

    /// <summary>A request the caller did not send reads as "not found", like in <see cref="GetPendingRequestToAsync"/>.</summary>
    public async Task CancelRequestAsync(int requestId, int senderId, CancellationToken ct)
    {
        var request = await friends.GetByIdAsync(requestId, ct);
        if (request is null || request.SenderId != senderId)
            throw new NotFoundException(RequestNotFoundMessage);
        if (request.Status != FriendRequestStatus.Pending)
            throw new ConflictException("Only pending requests can be cancelled.");

        await friends.DeleteAsync(request.Id, ct);
    }

    /// <summary>At most one row exists per pair of users, in either direction (unique index).</summary>
    private async Task<FriendRequest> CreateOrReopenAsync(FriendRequestAttempt attempt, CancellationToken ct)
    {
        var existing = await friends.GetExistingAsync(attempt.SenderId, attempt.Target.Id, ct);
        if (existing == null)
            return await CreatePendingAsync(attempt, ct);

        return existing.Status switch
        {
            FriendRequestStatus.Accepted => throw new ConflictException("You are already friends."),
            FriendRequestStatus.Pending => throw new ConflictException(AlreadyRequestedMessage),
            _ => await ReopenDeclinedAsync(existing, attempt, ct)
        };
    }

    /// <summary>A concurrent request for the same pair (double click, or both users at once) loses on the unique index.</summary>
    private async Task<FriendRequest> CreatePendingAsync(FriendRequestAttempt attempt, CancellationToken ct)
    {
        try
        {
            return await friends.CreateAsync(new FriendRequest
            {
                SenderId = attempt.SenderId,
                ReceiverId = attempt.Target.Id,
                Status = FriendRequestStatus.Pending,
                CreatedAt = attempt.Now
            }, ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            throw new ConflictException(AlreadyRequestedMessage);
        }
    }

    /// <summary>The declined sender waits out the cooldown; the user who declined may ask right away.</summary>
    private async Task<FriendRequest> ReopenDeclinedAsync(FriendRequest declined, FriendRequestAttempt attempt, CancellationToken ct)
    {
        if (IsDeclinedSenderCoolingDown(declined, attempt))
            throw new ConflictException($"{attempt.Target.Username} declined your friend request recently. Try again after {RerequestCooldown.TotalHours:0} hours.");

        if (!await friends.ReopenDeclinedAsync(declined.Id, new FriendRequestDirection(attempt.SenderId, attempt.Target.Id), attempt.Now, ct))
            throw new ConflictException(AlreadyRequestedMessage);
        return await friends.GetByIdAsync(declined.Id, ct)
            ?? throw new ConflictException(AlreadyRequestedMessage);
    }

    private static bool IsDeclinedSenderCoolingDown(FriendRequest declined, FriendRequestAttempt attempt)
    {
        var declinedAt = declined.UpdatedAt ?? declined.CreatedAt;
        return declined.SenderId == attempt.SenderId && declinedAt > attempt.Now - RerequestCooldown;
    }

    /// <summary>
    /// Prevents notification spam (request, cancel, request again) within <see cref="NotificationCooldown"/>.
    /// Runs after the request is saved, so it uses <see cref="CancellationToken.None"/>.
    /// </summary>
    private async Task NotifyTargetUnlessRecentlyNotifiedAsync(FriendRequestAttempt attempt, string senderUsername)
    {
        var wasRecentlyNotified = await notifications.HasRecentUnreadFromActorAsync(new RecentNotificationQuery(
            attempt.Target.Id, NotificationTypes.FriendRequest, attempt.SenderId, attempt.Now - NotificationCooldown), CancellationToken.None);
        if (wasRecentlyNotified) return;

        await notifications.CreateBatchAsync([FriendNotifications.RequestReceived(attempt, senderUsername)], CancellationToken.None);
    }

    /// <summary>Wrong owner reads as "not found" so callers cannot probe which request ids exist.</summary>
    private async Task<FriendRequest> GetPendingRequestToAsync(int requestId, int receiverId, CancellationToken ct)
    {
        var request = await friends.GetByIdAsync(requestId, ct);
        if (request is null || request.ReceiverId != receiverId)
            throw new NotFoundException(RequestNotFoundMessage);
        if (request.Status != FriendRequestStatus.Pending)
            throw new ConflictException("This request is no longer pending.");
        return request;
    }

    private async Task SaveStatusAsync(FriendRequest request, FriendRequestStatus status, DateTime now, CancellationToken ct)
    {
        request.Status = status;
        request.UpdatedAt = now;
        await friends.UpdateAsync(request, ct);
    }

    /// <summary>Runs after the acceptance is saved, so it uses <see cref="CancellationToken.None"/>.</summary>
    private async Task NotifySenderOfAcceptanceAsync(FriendRequest request, DateTime now)
    {
        var receiver = await users.GetByIdAsync(request.ReceiverId, CancellationToken.None);
        await notifications.CreateBatchAsync([FriendNotifications.RequestAccepted(request, receiver?.Username, now)], CancellationToken.None);
    }
}

/// <summary>One user asking another to be friends, at <see cref="Now"/>.</summary>
internal sealed record FriendRequestAttempt(int SenderId, User Target, DateTime Now);

/// <summary>Builds the notifications the friend flow sends.</summary>
internal static class FriendNotifications
{
    private const string UnknownUsername = "Someone";

    public static Notification RequestReceived(FriendRequestAttempt attempt, string senderUsername) => new()
    {
        UserId = attempt.Target.Id,
        GameId = null,
        ScoringDay = attempt.Now.Date,
        Message = $"{senderUsername} sent you a friend request.",
        Type = NotificationTypes.FriendRequest,
        ActorUserId = attempt.SenderId,
        CreatedAt = attempt.Now
    };

    public static Notification RequestAccepted(FriendRequest request, string? receiverUsername, DateTime now) => new()
    {
        UserId = request.SenderId,
        GameId = null,
        ScoringDay = now.Date,
        Message = $"{receiverUsername ?? UnknownUsername} accepted your friend request.",
        Type = NotificationTypes.FriendRequestAccepted,
        ActorUserId = request.ReceiverId,
        CreatedAt = now
    };
}
