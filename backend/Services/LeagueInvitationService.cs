using DailyChallenges.Data;
using DailyChallenges.DTOs;
using DailyChallenges.Exceptions;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Services;

public sealed class LeagueInvitationService(
    ILeagueRepository leagues,
    ILeagueInvitationRepository invitations,
    IUserProfileRepository users,
    INotificationRepository notifications,
    ILeagueService leagueViews,
    TimeProvider time) : ILeagueInvitationService
{
    /// <summary>How long after a declined invitation the same user cannot be invited to the league again.</summary>
    public static readonly TimeSpan ReinviteCooldown = TimeSpan.FromHours(24);

    /// <summary>How long an invite link can be used.</summary>
    public static readonly TimeSpan InviteLinkLifetime = TimeSpan.FromDays(7);

    private const int MaxInviteTokenAttempts = 3;
    private const int InviteTokenLength = 22;
    private const string InvitationNotFound = "Invitation not found.";
    private const string InvitationNotPending = "This invitation is no longer pending.";
    private const string InviteLinkUnavailable = "Invite link not found or already used.";

    public async Task<LeagueInvitationDto> InviteByUsernameAsync(int leagueId, int inviterId, string username, CancellationToken ct)
    {
        var league = await leagues.GetExistingWithMembersAsync(leagueId, ct);
        league.RequireOwner(inviterId);
        var invitee = await users.GetByUsernameAsync(username, ct)
            ?? throw new NotFoundException($"User '{username}' not found.");

        RequireNotSelfOrMember(league, invitee, username);
        await RequireNoPendingInvitationAsync(leagueId, invitee, ct);
        var nowUtc = UtcNow;
        await RequireReinviteCooldownElapsedAsync(leagueId, invitee, nowUtc, ct);
        var inviter = await users.GetByIdAsync(inviterId, ct)
            ?? throw new NotFoundException("Inviter not found.");

        var invitation = await CreateDirectInvitationAsync(
            new LeagueInvitation
            {
                LeagueId = leagueId,
                InviterId = inviterId,
                InviteeId = invitee.Id,
                Status = LeagueInvitationStatus.Pending,
                CreatedAt = nowUtc
            },
            invitee, ct);
        await NotifyInviteeAsync(league, inviter, invitee);

        invitation.League = league;
        invitation.Inviter = inviter;
        invitation.Invitee = invitee;
        return LeagueDtoMapper.ToDto(invitation);
    }

    public async Task<LeagueInvitationDto> CreateInviteLinkAsync(int leagueId, int inviterId, CancellationToken ct)
    {
        var league = await leagues.GetExistingWithMembersAsync(leagueId, ct);
        league.RequireOwner(inviterId);

        var invitation = await CreateLinkInvitationAsync(leagueId, inviterId, ct);

        invitation.League = league;
        invitation.Inviter = await users.GetByIdAsync(inviterId, ct);
        return LeagueDtoMapper.ToDto(invitation);
    }

    public async Task<LeagueDetailDto> AcceptInvitationAsync(int invitationId, int userId, CancellationToken ct)
    {
        var invitation = await GetPendingInvitationSentToAsync(invitationId, userId, ct);
        return await JoinAsync(invitation, userId, InvitationNotPending, ct);
    }

    public async Task<LeagueDetailDto> JoinByTokenAsync(string token, int userId, CancellationToken ct)
    {
        var invitation = await invitations.GetInvitationByTokenAsync(token, ct)
            ?? throw new NotFoundException(InviteLinkUnavailable);
        if (invitation.ExpiresAt < UtcNow)
            throw new ValidationException("This invite link has expired.");

        return await JoinAsync(invitation, userId, InviteLinkUnavailable, ct);
    }

    public async Task DeclineInvitationAsync(int invitationId, int userId, CancellationToken ct)
    {
        var invitation = await GetPendingInvitationSentToAsync(invitationId, userId, ct);
        await CloseAsync(invitation, LeagueInvitationStatus.Declined, ct);
    }

    public async Task CancelInvitationAsync(int invitationId, int requestingUserId, CancellationToken ct)
    {
        var invitation = await invitations.GetInvitationByIdAsync(invitationId, ct)
            ?? throw new NotFoundException(InvitationNotFound);
        var league = await leagues.GetExistingAsync(invitation.LeagueId, ct);
        if (league.OwnerId != requestingUserId)
            throw new ForbiddenException("Only the league owner can cancel invitations.");
        RequirePending(invitation);

        await CloseAsync(invitation, LeagueInvitationStatus.Cancelled, ct);
    }

    public async Task<List<LeagueInvitationDto>> GetMyPendingInvitationsAsync(int userId, CancellationToken ct)
    {
        var pending = await invitations.GetPendingInvitationsForUserAsync(userId, ct);
        return pending.Select(LeagueDtoMapper.ToDto).ToList();
    }

    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    private static void RequireNotSelfOrMember(League league, User invitee, string username)
    {
        if (invitee.Id == league.OwnerId)
            throw new ValidationException("You cannot invite yourself.");
        if (league.Members.Any(m => m.UserId == invitee.Id))
            throw new ConflictException($"{username} is already a member of this league.");
    }

    private async Task RequireNoPendingInvitationAsync(int leagueId, User invitee, CancellationToken ct)
    {
        if (await invitations.GetPendingInvitationAsync(leagueId, invitee.Id, ct) != null)
            throw PendingInvitationExists(invitee);
    }

    private async Task RequireReinviteCooldownElapsedAsync(int leagueId, User invitee, DateTime nowUtc, CancellationToken ct)
    {
        var lastDeclinedAt = await invitations.GetLastDeclinedAtAsync(leagueId, invitee.Id, ct);
        if (lastDeclinedAt > nowUtc - ReinviteCooldown)
            throw new ConflictException($"{invitee.Username} declined an invitation to this league recently. Try again after {ReinviteCooldown.TotalHours:0} hours.");
    }

    /// <summary>The filtered unique index also catches a concurrent request that created the pending invite first.</summary>
    private async Task<LeagueInvitation> CreateDirectInvitationAsync(LeagueInvitation invitation, User invitee, CancellationToken ct)
    {
        try
        {
            return await invitations.CreateInvitationAsync(invitation, ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            throw PendingInvitationExists(invitee);
        }
    }

    private static ConflictException PendingInvitationExists(User invitee) =>
        new($"A pending invitation to {invitee.Username} already exists.");

    /// <summary>Tokens have a unique index. A collision of random GUIDs is practically impossible, but retry anyway.</summary>
    private async Task<LeagueInvitation> CreateLinkInvitationAsync(int leagueId, int inviterId, CancellationToken ct)
    {
        var nowUtc = UtcNow;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await invitations.CreateInvitationAsync(new LeagueInvitation
                {
                    LeagueId = leagueId,
                    InviterId = inviterId,
                    InviteeId = null,
                    Token = GenerateToken(),
                    Status = LeagueInvitationStatus.Pending,
                    CreatedAt = nowUtc,
                    ExpiresAt = nowUtc + InviteLinkLifetime
                }, ct);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation() && attempt < MaxInviteTokenAttempts)
            {
            }
        }
    }

    private static string GenerateToken() =>
        Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", "-").Replace("/", "_").Replace("=", "")
            .Substring(0, InviteTokenLength);

    private async Task<LeagueInvitation> GetPendingInvitationSentToAsync(int invitationId, int userId, CancellationToken ct)
    {
        var invitation = await invitations.GetInvitationByIdAsync(invitationId, ct)
            ?? throw new NotFoundException(InvitationNotFound);
        if (invitation.InviteeId != userId)
            throw new ForbiddenException("This invitation was not sent to you.");
        RequirePending(invitation);
        return invitation;
    }

    private static void RequirePending(LeagueInvitation invitation)
    {
        if (invitation.Status != LeagueInvitationStatus.Pending)
            throw new ConflictException(InvitationNotPending);
    }

    private async Task CloseAsync(LeagueInvitation invitation, LeagueInvitationStatus status, CancellationToken ct)
    {
        if (!await invitations.TryCloseInvitationAsync(invitation.Id, status, UtcNow, ct))
            throw new ConflictException(InvitationNotPending);
    }

    /// <summary>The repository claims the invitation and adds the membership atomically, so a concurrent accept fails.</summary>
    private async Task<LeagueDetailDto> JoinAsync(LeagueInvitation invitation, int userId, string notPendingMessage, CancellationToken ct)
    {
        var league = await leagues.GetExistingAsync(invitation.LeagueId, ct);

        var result = await invitations.AcceptInvitationAsync(invitation.Id, userId, UtcNow, ct);
        if (result == AcceptInvitationResult.AlreadyMember)
            throw new ConflictException("You are already a member of this league.");
        if (result == AcceptInvitationResult.NotPending)
            throw new ConflictException(notPendingMessage);

        await NotifyOwnerOfJoinAsync(league, userId);
        return await leagueViews.GetLeagueDetailAsync(league.Id, userId, ct);
    }

    private async Task NotifyInviteeAsync(League league, User inviter, User invitee) =>
        await SendNotificationAsync(new Notification
        {
            UserId = invitee.Id,
            Message = $"{inviter.Username} invited you to join the league \"{league.Name}\".",
            Type = NotificationTypes.LeagueInvite,
            ActorUserId = inviter.Id
        });

    private async Task NotifyOwnerOfJoinAsync(League league, int joinerId)
    {
        var joiner = await users.GetByIdAsync(joinerId, CancellationToken.None);
        await SendNotificationAsync(new Notification
        {
            UserId = league.OwnerId,
            Message = $"{joiner?.Username ?? DisplayNames.Someone} joined your league \"{league.Name}\".",
            Type = NotificationTypes.LeagueJoined,
            ActorUserId = joinerId
        });
    }

    /// <summary>
    /// Notifications follow an invitation or membership that is already saved, so they are sent with
    /// <see cref="CancellationToken.None"/>: a client abort must not leave the saved change unannounced.
    /// </summary>
    private async Task SendNotificationAsync(Notification notification)
    {
        var nowUtc = UtcNow;
        notification.GameId = null;
        notification.ScoringDay = nowUtc.Date;
        notification.CreatedAt = nowUtc;
        await notifications.CreateBatchAsync([notification], CancellationToken.None);
    }
}
