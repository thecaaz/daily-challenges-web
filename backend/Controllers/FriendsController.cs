using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DailyChallenges.DTOs;
using DailyChallenges.Services;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Controllers
{
    [ApiController]
    [Route("api/friends")]
    [Authorize]
    public class FriendsController : ControllerBase
    {
        private readonly IFriendService _friends;

        public FriendsController(IFriendService friends)
        {
            _friends = friends;
        }

        // ── Send request by userId ────────────────────────────────────────────

        [HttpPost("requests")]
        public async Task<ActionResult<FriendRequestDto>> SendRequest([FromBody] SendFriendRequestDto body)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            var result = await _friends.SendRequestAsync(userId.Value, body.TargetUserId);
            return Ok(result);
        }

        // ── Send request by username ──────────────────────────────────────────

        [HttpPost("requests/by-username")]
        public async Task<ActionResult<FriendRequestDto>> SendRequestByUsername([FromBody] SendFriendRequestByUsernameDto body)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            var result = await _friends.SendRequestByUsernameAsync(userId.Value, body.Username);
            return Ok(result);
        }

        // ── Incoming / outgoing lists ─────────────────────────────────────────

        [HttpGet("requests/incoming")]
        public async Task<ActionResult<List<FriendRequestDto>>> GetIncoming()
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            return Ok(await _friends.GetIncomingRequestsAsync(userId.Value));
        }

        [HttpGet("requests/sent")]
        public async Task<ActionResult<List<FriendRequestDto>>> GetSent()
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            return Ok(await _friends.GetOutgoingRequestsAsync(userId.Value));
        }

        // ── Accept / reject ───────────────────────────────────────────────────

        [HttpPost("requests/{id:int}/accept")]
        public async Task<IActionResult> Accept(int id)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            await _friends.RespondToRequestAsync(id, userId.Value, accept: true);
            return NoContent();
        }

        [HttpPost("requests/{id:int}/reject")]
        public async Task<IActionResult> Reject(int id)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            await _friends.RespondToRequestAsync(id, userId.Value, accept: false);
            return NoContent();
        }

        // ── Cancel own pending request ────────────────────────────────────────

        [HttpDelete("requests/{id:int}")]
        public async Task<IActionResult> CancelRequest(int id)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            await _friends.CancelRequestAsync(id, userId.Value);
            return NoContent();
        }

        // ── Friends list ──────────────────────────────────────────────────────

        [HttpGet]
        public async Task<ActionResult<List<FriendDto>>> GetFriends()
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            return Ok(await _friends.GetFriendsAsync(userId.Value));
        }

        [HttpDelete("{friendId:int}")]
        public async Task<IActionResult> RemoveFriend(int friendId)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            await _friends.RemoveFriendAsync(userId.Value, friendId);
            return NoContent();
        }

        // ── Relationship status (auth optional) ───────────────────────────────

        [HttpGet("status/{targetUserId:int}")]
        [AllowAnonymous]
        public async Task<ActionResult<RelationshipStatusDto>> GetStatus(int targetUserId)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Ok(new RelationshipStatusDto { Status = "none" });
            var status = await _friends.GetRelationshipStatusAsync(userId.Value, targetUserId);
            return Ok(new RelationshipStatusDto { Status = status });
        }
    }

    public class SendFriendRequestDto
    {
        public int TargetUserId { get; set; }
    }

    public class SendFriendRequestByUsernameDto
    {
        public string Username { get; set; } = string.Empty;
    }

    public class RelationshipStatusDto
    {
        public string Status { get; set; } = string.Empty;
    }
}
