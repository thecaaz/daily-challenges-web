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
    public async Task<ActionResult<List<LeagueDto>>> GetMyLeagues([CurrentUserId] int userId, CancellationToken ct) =>
        Ok(await leagues.GetMyLeaguesAsync(userId, ct));

    [HttpPost]
    public async Task<ActionResult<LeagueDto>> Create([CurrentUserId] int userId, [FromBody] LeagueCreateRequest body, CancellationToken ct)
    {
        var league = await leagues.CreateLeagueAsync(userId, body.Name, ct);
        return CreatedAtAction(nameof(GetDetail), new { id = league.Id }, league);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LeagueDetailDto>> GetDetail([CurrentUserId] int userId, int id, CancellationToken ct) =>
        Ok(await leagues.GetLeagueDetailAsync(id, userId, ct));

    [HttpPatch("{id:int}/name")]
    public async Task<ActionResult<LeagueDto>> Rename([CurrentUserId] int userId, int id, [FromBody] LeagueRenameRequest body, CancellationToken ct) =>
        Ok(await leagues.RenameLeagueAsync(id, userId, body.Name, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete([CurrentUserId] int userId, int id, CancellationToken ct)
    {
        await leagues.DeleteLeagueAsync(id, userId, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/invitations")]
    public async Task<ActionResult<LeagueInvitationDto>> InviteByUsername([CurrentUserId] int userId, int id, [FromBody] LeagueInviteByUsernameRequest body, CancellationToken ct) =>
        Ok(await invitations.InviteByUsernameAsync(id, userId, body.Username, ct));

    [HttpPost("{id:int}/invite-link")]
    public async Task<ActionResult<LeagueInvitationDto>> CreateInviteLink([CurrentUserId] int userId, int id, CancellationToken ct) =>
        Ok(await invitations.CreateInviteLinkAsync(id, userId, ct));

    [HttpDelete("invitations/{invitationId:int}")]
    public async Task<IActionResult> CancelInvitation([CurrentUserId] int userId, int invitationId, CancellationToken ct)
    {
        await invitations.CancelInvitationAsync(invitationId, userId, ct);
        return NoContent();
    }

    [HttpGet("invitations/pending")]
    public async Task<ActionResult<List<LeagueInvitationDto>>> GetMyPendingInvitations([CurrentUserId] int userId, CancellationToken ct) =>
        Ok(await invitations.GetMyPendingInvitationsAsync(userId, ct));

    [HttpPost("invitations/{invitationId:int}/accept")]
    public async Task<ActionResult<LeagueDetailDto>> AcceptInvitation([CurrentUserId] int userId, int invitationId, CancellationToken ct) =>
        Ok(await invitations.AcceptInvitationAsync(invitationId, userId, ct));

    [HttpPost("invitations/{invitationId:int}/decline")]
    public async Task<IActionResult> DeclineInvitation([CurrentUserId] int userId, int invitationId, CancellationToken ct)
    {
        await invitations.DeclineInvitationAsync(invitationId, userId, ct);
        return NoContent();
    }

    [HttpPost("join/{token}")]
    public async Task<ActionResult<LeagueDetailDto>> JoinByToken([CurrentUserId] int userId, string token, CancellationToken ct) =>
        Ok(await invitations.JoinByTokenAsync(token, userId, ct));

    [HttpPost("{id:int}/leave")]
    public async Task<IActionResult> Leave([CurrentUserId] int userId, int id, CancellationToken ct)
    {
        await leagues.LeaveLeagueAsync(id, userId, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}/members/{memberId:int}")]
    public async Task<IActionResult> KickMember([CurrentUserId] int userId, int id, int memberId, CancellationToken ct)
    {
        await leagues.KickMemberAsync(id, userId, memberId, ct);
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
        [FromQuery] string? scoringDay = null, CancellationToken ct = default)
    {
        var query = new LeagueLeaderboardQuery(id, gameId, ParseDateOrNull(scoringDay), userId, User.IsAdmin());
        return Ok(await standings.GetLeaderboardAsync(query, ct));
    }

    [HttpGet("{id:int}/game-summaries")]
    public async Task<IActionResult> GetGameSummaries(
        [CurrentUserId] int userId,
        int id,
        [FromQuery] int days = DefaultSummaryDays,
        [FromQuery] int page = Paging.FirstPage,
        [FromQuery] int pageSize = DefaultSummaryPageSize, CancellationToken ct = default)
    {
        var (items, total) = await standings.GetLeagueGameSummariesAsync(new LeagueGameSummaryQuery(id, userId, days, page, pageSize), ct);
        return Ok(new { items, totalCount = total });
    }

    private static DateTime? ParseDateOrNull(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.Date
            : null;
}
