using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DailyChallenges.DTOs;
using DailyChallenges.Services;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Controllers;

[ApiController]
[Route("api/friends")]
[Authorize]
public sealed class FriendsController(
    IFriendRequestService requests,
    IFriendshipService friendships) : ControllerBase
{
    [HttpPost("requests")]
    public async Task<ActionResult<FriendRequestDto>> SendRequest([CurrentUserId] int userId, [FromBody] SendFriendRequestDto body, CancellationToken ct) =>
        Ok(await requests.SendRequestAsync(userId, body.TargetUserId, ct));

    [HttpPost("requests/by-username")]
    public async Task<ActionResult<FriendRequestDto>> SendRequestByUsername([CurrentUserId] int userId, [FromBody] SendFriendRequestByUsernameDto body, CancellationToken ct) =>
        Ok(await requests.SendRequestByUsernameAsync(userId, body.Username, ct));

    [HttpGet("requests/incoming")]
    public async Task<ActionResult<List<FriendRequestDto>>> GetIncoming([CurrentUserId] int userId, CancellationToken ct) =>
        Ok(await friendships.GetIncomingRequestsAsync(userId, ct));

    [HttpGet("requests/sent")]
    public async Task<ActionResult<List<FriendRequestDto>>> GetSent([CurrentUserId] int userId, CancellationToken ct) =>
        Ok(await friendships.GetOutgoingRequestsAsync(userId, ct));

    [HttpPost("requests/{id:int}/accept")]
    public async Task<IActionResult> Accept([CurrentUserId] int userId, int id, CancellationToken ct)
    {
        await requests.AcceptRequestAsync(id, userId, ct);
        return NoContent();
    }

    [HttpPost("requests/{id:int}/reject")]
    public async Task<IActionResult> Reject([CurrentUserId] int userId, int id, CancellationToken ct)
    {
        await requests.DeclineRequestAsync(id, userId, ct);
        return NoContent();
    }

    [HttpDelete("requests/{id:int}")]
    public async Task<IActionResult> CancelRequest([CurrentUserId] int userId, int id, CancellationToken ct)
    {
        await requests.CancelRequestAsync(id, userId, ct);
        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<List<FriendDto>>> GetFriends([CurrentUserId] int userId, CancellationToken ct) =>
        Ok(await friendships.GetFriendsAsync(userId, ct));

    [HttpDelete("{friendId:int}")]
    public async Task<IActionResult> RemoveFriend([CurrentUserId] int userId, int friendId, CancellationToken ct)
    {
        await friendships.RemoveFriendAsync(userId, friendId, ct);
        return NoContent();
    }

    /// <summary>Authentication is optional: anonymous callers have no relationship with anyone.</summary>
    [HttpGet("status/{targetUserId:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<RelationshipStatusDto>> GetStatus(int targetUserId, CancellationToken ct)
    {
        var status = User.GetUserId() is int userId
            ? await friendships.GetRelationshipStatusAsync(userId, targetUserId, ct)
            : RelationshipStatus.None;
        return Ok(new RelationshipStatusDto { Status = status });
    }
}
