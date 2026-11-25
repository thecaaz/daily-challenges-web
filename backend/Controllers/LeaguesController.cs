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
    private const int DefaultSummaryDays = 7;
    private const int DefaultSummaryPageSize = 20;

    [HttpGet]
    public async Task<ActionResult<List<LeagueDto>>> GetMyLeagues([CurrentUserId] int userId) =>
        Ok(await leagues.GetMyLeaguesAsync(userId));

    [HttpPost]
    public async Task<ActionResult<LeagueDto>> Create([CurrentUserId] int userId, [FromBody] LeagueCreateRequest body)
    {
        var league = await leagues.CreateLeagueAsync(userId, body.Name);
        return CreatedAtAction(nameof(GetDetail), new { id = league.Id }, league);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LeagueDetailDto>> GetDetail([CurrentUserId] int userId, int id) =>
        Ok(await leagues.GetLeagueDetailAsync(id, userId));

    [HttpPatch("{id:int}/name")]
    public async Task<ActionResult<LeagueDto>> Rename([CurrentUserId] int userId, int id, [FromBody] LeagueRenameRequest body) =>
        Ok(await leagues.RenameLeagueAsync(id, userId, body.Name));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete([CurrentUserId] int userId, int id)
    {
        await leagues.DeleteLeagueAsync(id, userId);
        return NoContent();
    }

    [HttpPost("{id:int}/invitations")]
    public async Task<ActionResult<LeagueInvitationDto>> InviteByUsername([CurrentUserId] int userId, int id, [FromBody] LeagueInviteByUsernameRequest body) =>
        Ok(await invitations.InviteByUsernameAsync(id, userId, body.Username));

    [HttpPost("{id:int}/invite-link")]
    public async Task<ActionResult<LeagueInvitationDto>> CreateInviteLink([CurrentUserId] int userId, int id) =>
        Ok(await invitations.CreateInviteLinkAsync(id, userId));

    [HttpDelete("invitations/{invitationId:int}")]
    public async Task<IActionResult> CancelInvitation([CurrentUserId] int userId, int invitationId)
    {
        await invitations.CancelInvitationAsync(invitationId, userId);
        return NoContent();
    }

    [HttpGet("invitations/pending")]
    public async Task<ActionResult<List<LeagueInvitationDto>>> GetMyPendingInvitations([CurrentUserId] int userId) =>
        Ok(await invitations.GetMyPendingInvitationsAsync(userId));

    [HttpPost("invitations/{invitationId:int}/accept")]
    public async Task<ActionResult<LeagueDetailDto>> AcceptInvitation([CurrentUserId] int userId, int invitationId) =>
        Ok(await invitations.AcceptInvitationAsync(invitationId, userId));

    [HttpPost("invitations/{invitationId:int}/decline")]
    public async Task<IActionResult> DeclineInvitation([CurrentUserId] int userId, int invitationId)
    {
        await invitations.DeclineInvitationAsync(invitationId, userId);
        return NoContent();
    }

    [HttpPost("join/{token}")]
    public async Task<ActionResult<LeagueDetailDto>> JoinByToken([CurrentUserId] int userId, string token) =>
        Ok(await invitations.JoinByTokenAsync(token, userId));

    [HttpPost("{id:int}/leave")]
    public async Task<IActionResult> Leave([CurrentUserId] int userId, int id)
    {
        await leagues.LeaveLeagueAsync(id, userId);
        return NoContent();
    }

    [HttpDelete("{id:int}/members/{memberId:int}")]
    public async Task<IActionResult> KickMember([CurrentUserId] int userId, int id, int memberId)
    {
        await leagues.KickMemberAsync(id, userId, memberId);
        return NoContent();
    }

    /// <param name="userId">The caller.</param>
    /// <param name="id">The league.</param>
    /// <param name="gameId">The game.</param>
    /// <param name="scoringDay">Omitted or unparseable: the game's current scoring day.</param>
    [HttpGet("{id:int}/leaderboard")]
    public async Task<ActionResult<LeagueLeaderboardDto>> GetLeaderboard(
        [CurrentUserId] int userId,
        int id,
        [FromQuery] int gameId,
        [FromQuery] string? scoringDay = null)
    {
        var query = new LeagueLeaderboardQuery(id, gameId, ParseDateOrNull(scoringDay), userId, User.IsAdmin());
        return Ok(await standings.GetLeaderboardAsync(query));
    }

    [HttpGet("{id:int}/game-summaries")]
    public async Task<IActionResult> GetGameSummaries(
        [CurrentUserId] int userId,
        int id,
        [FromQuery] int days = DefaultSummaryDays,
        [FromQuery] int page = Paging.FirstPage,
        [FromQuery] int pageSize = DefaultSummaryPageSize)
    {
        var (items, total) = await standings.GetLeagueGameSummariesAsync(new LeagueGameSummaryQuery(id, userId, days, page, pageSize));
        return Ok(new { items, totalCount = total });
    }

    private static DateTime? ParseDateOrNull(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.Date
            : null;
}
