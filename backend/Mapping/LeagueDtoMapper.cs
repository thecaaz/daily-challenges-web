using DailyChallenges.DTOs;
using DailyChallenges.Models;
using DailyChallenges.Services;

namespace DailyChallenges.Mapping;

public static class LeagueDtoMapper
{
    /// <summary>Shown for a member whose user row was not loaded.</summary>
    private const int StartingLevel = 1;

    public static LeagueDto ToDto(League league) => ToDto(league, league.Owner?.Username ?? string.Empty);

    /// <summary>For a league whose <see cref="League.Owner"/> was not loaded.</summary>
    public static LeagueDto ToDto(League league, string ownerUsername) =>
        new()
        {
            Id = league.Id,
            Name = league.Name,
            OwnerId = league.OwnerId,
            OwnerUsername = ownerUsername,
            MemberCount = league.Members.Count,
            CreatedAt = league.CreatedAt
        };

    /// <param name="league">Loaded with owner and members (with users).</param>
    /// <param name="visibleInvitations">The pending invitations the requester may see.</param>
    /// <param name="nowUtc">Decides each member's effective streak.</param>
    public static LeagueDetailDto ToDetailDto(League league, IEnumerable<LeagueInvitation> visibleInvitations, DateTime nowUtc) =>
        new()
        {
            Id = league.Id,
            Name = league.Name,
            OwnerId = league.OwnerId,
            OwnerUsername = league.Owner?.Username ?? string.Empty,
            CreatedAt = league.CreatedAt,
            Members = league.Members.Select(member => ToDto(member, nowUtc)).ToList(),
            PendingInvitations = visibleInvitations.Select(ToDto).ToList()
        };

    public static LeagueInvitationDto ToDto(LeagueInvitation invitation) =>
        new()
        {
            Id = invitation.Id,
            LeagueId = invitation.LeagueId,
            LeagueName = invitation.League?.Name ?? string.Empty,
            InviterId = invitation.InviterId,
            InviterUsername = invitation.Inviter?.Username ?? string.Empty,
            InviteeId = invitation.InviteeId,
            InviteeUsername = invitation.Invitee?.Username,
            Token = invitation.Token,
            Status = invitation.Status.ToString().ToLowerInvariant(),
            CreatedAt = invitation.CreatedAt,
            ExpiresAt = invitation.ExpiresAt
        };

    private static LeagueMemberDto ToDto(LeagueMember member, DateTime nowUtc) =>
        new()
        {
            UserId = member.UserId,
            Username = member.User?.Username ?? string.Empty,
            Level = member.User?.Level ?? StartingLevel,
            Streak = member.User != null ? StreakCalculator.GetEffectiveStreak(member.User, nowUtc) : 0,
            Role = member.Role.ToString().ToLowerInvariant(),
            JoinedAt = member.JoinedAt
        };
}
