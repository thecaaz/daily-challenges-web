using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DailyChallenges.DTOs;
using DailyChallenges.Services;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Controllers
{
    [ApiController]
    [Route("api/leagues")]
    [Authorize]
    public class LeaguesController : ControllerBase
    {
        private readonly ILeagueService _leagues;

        public LeaguesController(ILeagueService leagues)
        {
            _leagues = leagues;
        }

        // ── My leagues ────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<ActionResult<List<LeagueDto>>> GetMyLeagues()
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            return Ok(await _leagues.GetMyLeaguesAsync(userId.Value));
        }

        // ── Create ────────────────────────────────────────────────────────────

        [HttpPost]
        public async Task<ActionResult<LeagueDto>> Create([FromBody] LeagueCreateRequest body)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            var result = await _leagues.CreateLeagueAsync(userId.Value, body.Name);
            return CreatedAtAction(nameof(GetDetail), new { id = result.Id }, result);
        }

        // ── Detail ────────────────────────────────────────────────────────────

        [HttpGet("{id:int}")]
        public async Task<ActionResult<LeagueDetailDto>> GetDetail(int id)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            return Ok(await _leagues.GetLeagueDetailAsync(id, userId.Value));
        }

        // ── Rename ────────────────────────────────────────────────────────────

        [HttpPatch("{id:int}/name")]
        public async Task<ActionResult<LeagueDto>> Rename(int id, [FromBody] LeagueRenameRequest body)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            return Ok(await _leagues.RenameLeagueAsync(id, userId.Value, body.Name));
        }

        // ── Delete ────────────────────────────────────────────────────────────

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            await _leagues.DeleteLeagueAsync(id, userId.Value);
            return NoContent();
        }

        // ── Invite by username ────────────────────────────────────────────────

        [HttpPost("{id:int}/invitations")]
        public async Task<ActionResult<LeagueInvitationDto>> InviteByUsername(int id, [FromBody] LeagueInviteByUsernameRequest body)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            return Ok(await _leagues.InviteByUsernameAsync(id, userId.Value, body.Username));
        }

        // ── Create invite link (token-based) ─────────────────────────────────

        [HttpPost("{id:int}/invite-link")]
        public async Task<ActionResult<LeagueInvitationDto>> CreateInviteLink(int id)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            return Ok(await _leagues.CreateInviteLinkAsync(id, userId.Value));
        }

        // ── Cancel invitation ─────────────────────────────────────────────────

        [HttpDelete("invitations/{invitationId:int}")]
        public async Task<IActionResult> CancelInvitation(int invitationId)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            await _leagues.CancelInvitationAsync(invitationId, userId.Value);
            return NoContent();
        }

        // ── My pending invitations ────────────────────────────────────────────

        [HttpGet("invitations/pending")]
        public async Task<ActionResult<List<LeagueInvitationDto>>> GetMyPendingInvitations()
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            return Ok(await _leagues.GetMyPendingInvitationsAsync(userId.Value));
        }

        // ── Accept invitation ─────────────────────────────────────────────────

        [HttpPost("invitations/{invitationId:int}/accept")]
        public async Task<ActionResult<LeagueDetailDto>> AcceptInvitation(int invitationId)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            return Ok(await _leagues.AcceptInvitationAsync(invitationId, userId.Value));
        }

        // ── Decline invitation ────────────────────────────────────────────────

        [HttpPost("invitations/{invitationId:int}/decline")]
        public async Task<IActionResult> DeclineInvitation(int invitationId)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            await _leagues.DeclineInvitationAsync(invitationId, userId.Value);
            return NoContent();
        }

        // ── Join by invite link token ─────────────────────────────────────────

        [HttpPost("join/{token}")]
        public async Task<ActionResult<LeagueDetailDto>> JoinByToken(string token)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            return Ok(await _leagues.JoinByTokenAsync(token, userId.Value));
        }

        // ── Leave ─────────────────────────────────────────────────────────────

        [HttpPost("{id:int}/leave")]
        public async Task<IActionResult> Leave(int id)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            await _leagues.LeaveLeagueAsync(id, userId.Value);
            return NoContent();
        }

        // ── Kick member ───────────────────────────────────────────────────────

        [HttpDelete("{id:int}/members/{memberId:int}")]
        public async Task<IActionResult> KickMember(int id, int memberId)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            await _leagues.KickMemberAsync(id, userId.Value, memberId);
            return NoContent();
        }

        // ── Leaderboard ───────────────────────────────────────────────────────

        [HttpGet("{id:int}/leaderboard")]
        public async Task<ActionResult<LeagueLeaderboardDto>> GetLeaderboard(
            int id,
            [FromQuery] int gameId,
            [FromQuery] string? scoringDay = null)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();

            var day = DateTime.TryParse(scoringDay, out var parsed)
                ? parsed.Date
                : DateTime.UtcNow.Date;

            return Ok(await _leagues.GetLeaderboardAsync(id, gameId, day, userId.Value));
        }

        [HttpGet("{id:int}/game-summaries")]
        public async Task<IActionResult> GetGameSummaries(int id, [FromQuery] int days = 7, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();
            var (items, total) = await _leagues.GetLeagueGameSummariesAsync(id, userId.Value, days, page, pageSize);
            return Ok(new { items, totalCount = total });
        }
    }
}
