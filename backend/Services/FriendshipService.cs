using DailyChallenges.DTOs;
using DailyChallenges.Exceptions;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Services;

public sealed class FriendshipService(
    IFriendRepository friends,
    LevelCalculator levels,
    TimeProvider time) : IFriendshipService
{
    private const string FriendshipNotFoundMessage = "Friendship not found.";

    public async Task<List<FriendRequestDto>> GetIncomingRequestsAsync(int userId, CancellationToken ct)
    {
        var requests = await friends.GetIncomingPendingAsync(userId, ct);
        return requests.Select(DtoMapper.ToDto).ToList();
    }

    public async Task<List<FriendRequestDto>> GetOutgoingRequestsAsync(int userId, CancellationToken ct)
    {
        var requests = await friends.GetOutgoingPendingAsync(userId, ct);
        return requests.Select(DtoMapper.ToDto).ToList();
    }

    public async Task<List<FriendDto>> GetFriendsAsync(int userId, CancellationToken ct)
    {
        var friendUsers = await friends.GetFriendsAsync(userId, ct);
        return friendUsers.Select(user => DtoMapper.ToFriendDto(user, levels, time.GetUtcNow().UtcDateTime)).ToList();
    }

    /// <summary>A declined request reads as no relationship.</summary>
    public async Task<string> GetRelationshipStatusAsync(int requestingUserId, int targetUserId, CancellationToken ct)
    {
        var request = await friends.GetRelationshipAsync(requestingUserId, targetUserId, ct);
        return request?.Status switch
        {
            FriendRequestStatus.Accepted => RelationshipStatus.Friends,
            FriendRequestStatus.Pending when request.SenderId == requestingUserId => RelationshipStatus.PendingSent,
            FriendRequestStatus.Pending => RelationshipStatus.PendingReceived,
            _ => RelationshipStatus.None
        };
    }

    public async Task RemoveFriendAsync(int userId, int friendId, CancellationToken ct)
    {
        var friendship = await friends.GetExistingAsync(userId, friendId, ct);
        if (friendship is null || friendship.Status != FriendRequestStatus.Accepted)
            throw new NotFoundException(FriendshipNotFoundMessage);

        await friends.DeleteAsync(friendship.Id, ct);
    }
}
