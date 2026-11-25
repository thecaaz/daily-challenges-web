using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DailyChallenges.DTOs;
using DailyChallenges.Services;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Controllers;

[ApiController]
[Route("api/leagues")]
[Authorize]
public sealed class LeaguesController(
    ILeagueService leagues,
    ILeagueInvitationService invitations,
    ILeagueStandingsService standings) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<LeagueDto>>> GetMyLeagues()
    {
        if (User.GetUserId() is not int userId) return Forbid();
        return Ok(await leagues.GetMyLeaguesAsync(userId));
    }

    [HttpPost]
    public async Task<ActionResult<LeagueDto>> Create([FromBody] LeagueCreateRequest body)
    {
        if (User.GetUserId() is not int userId) return Forbid();
        var league = await leagues.CreateLeagueAsync(userId, body.Name);
        return CreatedAtAction(nameof(GetDetail), new { id = league.Id }, league);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LeagueDetailDto>> GetDetail(int id)
    {
        if (User.GetUserId() is not int userId) return Forbid();
        return Ok(await leagues.GetLeagueDetailAsync(id, userId));
    }

    [HttpPatch("{id:int}/name")]
    public async Task<ActionResult<LeagueDto>> Rename(int id, [FromBody] LeagueRenameRequest body)
    {
        if (User.GetUserId() is not int userId) return Forbid();
        return Ok(await leagues.RenameLeagueAsync(id, userId, body.Name));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (User.GetUserId() is not int userId) return Forbid();
        await leagues.DeleteLeagueAsync(id, userId);
        return NoContent();
    }

    [HttpPost("{id:int}/invitations")]
    public async Task<ActionResult<LeagueInvitationDto>> InviteByUsername(int id, [FromBody] LeagueInviteByUsernameRequest body)
    {
        if (User.GetUserId() is not int userId) return Forbid();
        return Ok(await invitations.InviteByUsernameAsync(id, userId, body.Username));
    }

    [HttpPost("{id:int}/invite-link")]
    public async Task<ActionResult<LeagueInvitationDto>> CreateInviteLink(int id)
    {
        if (User.GetUserId() is not int userId) return Forbid();
        return Ok(await invitations.CreateInviteLinkAsync(id, userId));
    }

    [HttpDelete("invitations/{invitationId:int}")]
    public async Task<IActionResult> CancelInvitation(int invitationId)
    {
        if (User.GetUserId() is not int userId) return Forbid();
        await invitations.CancelInvitationAsync(invitationId, userId);
        return NoContent();
    }

    [HttpGet("invitations/pending")]
    public async Task<ActionResult<List<LeagueInvitationDto>>> GetMyPendingInvitations()
    {
        if (User.GetUserId() is not int userId) return Forbid();
        return Ok(await invitations.GetMyPendingInvitationsAsync(userId));
    }

    [HttpPost("invitations/{invitationId:int}/accept")]
    public async Task<ActionResult<LeagueDetailDto>> AcceptInvitation(int invitationId)
    {
        if (User.GetUserId() is not int userId) return Forbid();
        return Ok(await invitations.AcceptInvitationAsync(invitationId, userId));
    }

    [HttpPost("invitations/{invitationId:int}/decline")]
    public async Task<IActionResult> DeclineInvitation(int invitationId)
    {
        if (User.GetUserId() is not int userId) return Forbid();
        await invitations.DeclineInvitationAsync(invitationId, userId);
        return NoContent();
    }

    [HttpPost("join/{token}")]
    public async Task<ActionResult<LeagueDetailDto>> JoinByToken(string token)
    {
        if (User.GetUserId() is not int userId) return Forbid();
        return Ok(await invitations.JoinByTokenAsync(token, userId));
    }

    [HttpPost("{id:int}/leave")]
    public async Task<IActionResult> Leave(int id)
    {
        if (User.GetUserId() is not int userId) return Forbid();
        await leagues.LeaveLeagueAsync(id, userId);
        return NoContent();
    }

    [HttpDelete("{id:int}/members/{memberId:int}")]
    public async Task<IActionResult> KickMember(int id, int memberId)
    {
        if (User.GetUserId() is not int userId) return Forbid();
        await leagues.KickMemberAsync(id, userId, memberId);
        return NoContent();
    }

    /// <param name="id">The league.</param>
    /// <param name="gameId">The game.</param>
    /// <param name="scoringDay">Omitted or unparseable: the game's current scoring day.</param>
    [HttpGet("{id:int}/leaderboard")]
    public async Task<ActionResult<LeagueLeaderboardDto>> GetLeaderboard(
        int id,
        [FromQuery] int gameId,
        [FromQuery] string? scoringDay = null)
    {
        if (User.GetUserId() is not int userId) return Forbid();
        var query = new LeagueLeaderboardQuery(id, gameId, ParseDateOrNull(scoringDay), userId, User.IsAdmin());
        return Ok(await standings.GetLeaderboardAsync(query));
    }

    [HttpGet("{id:int}/game-summaries")]
    public async Task<IActionResult> GetGameSummaries(int id, [FromQuery] int days = 7, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (User.GetUserId() is not int userId) return Forbid();
        var (items, total) = await standings.GetLeagueGameSummariesAsync(new LeagueGameSummaryQuery(id, userId, days, page, pageSize));
        return Ok(new { items, totalCount = total });
    }

    private static DateTime? ParseDateOrNull(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.Date
            : null;
}
