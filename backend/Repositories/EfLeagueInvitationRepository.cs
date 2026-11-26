using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public sealed class EfLeagueInvitationRepository(AppDbContext db) : ILeagueInvitationRepository
{
    public async Task<LeagueInvitation?> GetInvitationByIdAsync(int invitationId, CancellationToken ct) =>
        await db.LeagueInvitations
            .Include(i => i.League)
            .Include(i => i.Inviter)
            .Include(i => i.Invitee)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invitationId, ct);

    public async Task<LeagueInvitation?> GetPendingInvitationAsync(int leagueId, int inviteeId, CancellationToken ct) =>
        await db.LeagueInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(i =>
                i.LeagueId == leagueId &&
                i.InviteeId == inviteeId &&
                i.Status == LeagueInvitationStatus.Pending, ct);

    public async Task<LeagueInvitation?> GetInvitationByTokenAsync(string token, CancellationToken ct) =>
        await db.LeagueInvitations
            .Include(i => i.League)
            .Include(i => i.Inviter)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Token == token && i.Status == LeagueInvitationStatus.Pending, ct);

    public async Task<List<LeagueInvitation>> GetPendingInvitationsForUserAsync(int userId, CancellationToken ct) =>
        await db.LeagueInvitations
            .Where(i => i.InviteeId == userId && i.Status == LeagueInvitationStatus.Pending)
            .Include(i => i.League!).ThenInclude(l => l.Owner)
            .Include(i => i.Inviter)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<DateTime?> GetLastDeclinedAtAsync(int leagueId, int inviteeId, CancellationToken ct) =>
        await db.LeagueInvitations
            .Where(i => i.LeagueId == leagueId && i.InviteeId == inviteeId && i.Status == LeagueInvitationStatus.Declined)
            .MaxAsync(i => i.UpdatedAt, ct);

    /// <inheritdoc />
    public async Task<LeagueInvitation> CreateInvitationAsync(LeagueInvitation invitation, CancellationToken ct)
    {
        db.LeagueInvitations.Add(invitation);
        await db.SaveInsertsOrDetachAsync([invitation], ct);
        return invitation;
    }

    /// <inheritdoc />
    public async Task<bool> TryCloseInvitationAsync(int invitationId, LeagueInvitationStatus status, DateTime nowUtc, CancellationToken ct) =>
        await TryMovePendingInvitationAsync(invitationId, status, nowUtc, ct);

    /// <inheritdoc />
    /// <remarks>
    /// SQLite runs the transaction as BEGIN IMMEDIATE, so concurrent accepts are serialized and the second
    /// one finds the invitation no longer pending. Returning without commit rolls the claim back, so an
    /// invite link is not used up by someone who is already a member.
    /// </remarks>
    public async Task<AcceptInvitationResult> AcceptInvitationAsync(int invitationId, int userId, DateTime nowUtc, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        if (!await TryMovePendingInvitationAsync(invitationId, LeagueInvitationStatus.Accepted, nowUtc, ct))
            return AcceptInvitationResult.NotPending;

        var leagueId = await GetLeagueIdAsync(invitationId, ct);
        if (!await TryAddMemberAsync(leagueId, userId, nowUtc, ct))
            return AcceptInvitationResult.AlreadyMember;

        await transaction.CommitAsync(ct);
        return AcceptInvitationResult.Accepted;
    }

    private async Task<bool> TryMovePendingInvitationAsync(int invitationId, LeagueInvitationStatus status, DateTime nowUtc, CancellationToken ct)
    {
        var updatedRows = await db.LeagueInvitations
            .Where(i => i.Id == invitationId && i.Status == LeagueInvitationStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Status, status)
                .SetProperty(i => i.UpdatedAt, (DateTime?)nowUtc), ct);
        return updatedRows == 1;
    }

    private async Task<int> GetLeagueIdAsync(int invitationId, CancellationToken ct) =>
        await db.LeagueInvitations
            .Where(i => i.Id == invitationId)
            .Select(i => i.LeagueId)
            .SingleAsync(ct);

    /// <summary>False, with the context left clean, when the user already is a member (unique violation).</summary>
    private Task<bool> TryAddMemberAsync(int leagueId, int userId, DateTime nowUtc, CancellationToken ct) =>
        db.TryInsertAsync(new LeagueMember
        {
            LeagueId = leagueId,
            UserId = userId,
            Role = LeagueRole.Member,
            JoinedAt = nowUtc
        }, ct);
}
