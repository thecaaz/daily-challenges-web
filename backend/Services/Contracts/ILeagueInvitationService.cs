using DailyChallenges.DTOs;

namespace DailyChallenges.Services.Contracts;

/// <summary>Inviting users to a league (by username or link) and answering those invitations.</summary>
public interface ILeagueInvitationService
{
    /// <summary>
    /// Owner only. Rejects self-invites, existing members, a second pending invitation, and re-invites within
    /// the cooldown after a decline. Notifies the invitee.
    /// </summary>
    Task<LeagueInvitationDto> InviteByUsernameAsync(int leagueId, int inviterId, string username);

    /// <summary>Owner only. Creates a token invitation that anyone can use until it expires.</summary>
    Task<LeagueInvitationDto> CreateInviteLinkAsync(int leagueId, int inviterId);

    /// <summary>The invitee joins the league; the owner is notified.</summary>
    Task<LeagueDetailDto> AcceptInvitationAsync(int invitationId, int userId);

    /// <summary>Joins the league through an unexpired invite link; the owner is notified.</summary>
    Task<LeagueDetailDto> JoinByTokenAsync(string token, int userId);

    Task DeclineInvitationAsync(int invitationId, int userId);

    /// <summary>Owner only.</summary>
    Task CancelInvitationAsync(int invitationId, int requestingUserId);

    Task<List<LeagueInvitationDto>> GetMyPendingInvitationsAsync(int userId);
}
