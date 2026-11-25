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
    public async Task<ActionResult<FriendRequestDto>> SendRequest([CurrentUserId] int userId, [FromBody] SendFriendRequestDto body) =>
        Ok(await requests.SendRequestAsync(userId, body.TargetUserId));

    [HttpPost("requests/by-username")]
    public async Task<ActionResult<FriendRequestDto>> SendRequestByUsername([CurrentUserId] int userId, [FromBody] SendFriendRequestByUsernameDto body) =>
        Ok(await requests.SendRequestByUsernameAsync(userId, body.Username));

    [HttpGet("requests/incoming")]
    public async Task<ActionResult<List<FriendRequestDto>>> GetIncoming([CurrentUserId] int userId) =>
        Ok(await friendships.GetIncomingRequestsAsync(userId));

    [HttpGet("requests/sent")]
    public async Task<ActionResult<List<FriendRequestDto>>> GetSent([CurrentUserId] int userId) =>
        Ok(await friendships.GetOutgoingRequestsAsync(userId));

    [HttpPost("requests/{id:int}/accept")]
    public async Task<IActionResult> Accept([CurrentUserId] int userId, int id)
    {
        await requests.AcceptRequestAsync(id, userId);
        return NoContent();
    }

    [HttpPost("requests/{id:int}/reject")]
    public async Task<IActionResult> Reject([CurrentUserId] int userId, int id)
    {
        await requests.DeclineRequestAsync(id, userId);
        return NoContent();
    }

    [HttpDelete("requests/{id:int}")]
    public async Task<IActionResult> CancelRequest([CurrentUserId] int userId, int id)
    {
        await requests.CancelRequestAsync(id, userId);
        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<List<FriendDto>>> GetFriends([CurrentUserId] int userId) =>
        Ok(await friendships.GetFriendsAsync(userId));

    [HttpDelete("{friendId:int}")]
    public async Task<IActionResult> RemoveFriend([CurrentUserId] int userId, int friendId)
    {
        await friendships.RemoveFriendAsync(userId, friendId);
        return NoContent();
    }

    /// <summary>Authentication is optional: anonymous callers have no relationship with anyone.</summary>
    [HttpGet("status/{targetUserId:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<RelationshipStatusDto>> GetStatus(int targetUserId)
    {
        var status = User.GetUserId() is int userId
            ? await friendships.GetRelationshipStatusAsync(userId, targetUserId)
            : RelationshipStatus.None;
        return Ok(new RelationshipStatusDto { Status = status });
    }
}
