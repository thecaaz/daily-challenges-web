using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public sealed class EfLeagueInvitationRepository(AppDbContext db) : ILeagueInvitationRepository
{
    public async Task<LeagueInvitation?> GetInvitationByIdAsync(int invitationId) =>
        await db.LeagueInvitations
            .Include(i => i.League)
            .Include(i => i.Inviter)
            .Include(i => i.Invitee)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invitationId);

    public async Task<LeagueInvitation?> GetPendingInvitationAsync(int leagueId, int inviteeId) =>
        await db.LeagueInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(i =>
                i.LeagueId == leagueId &&
                i.InviteeId == inviteeId &&
                i.Status == LeagueInvitationStatus.Pending);

    public async Task<LeagueInvitation?> GetInvitationByTokenAsync(string token) =>
        await db.LeagueInvitations
            .Include(i => i.League)
            .Include(i => i.Inviter)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Token == token && i.Status == LeagueInvitationStatus.Pending);

    public async Task<List<LeagueInvitation>> GetPendingInvitationsForUserAsync(int userId) =>
        await db.LeagueInvitations
            .Where(i => i.InviteeId == userId && i.Status == LeagueInvitationStatus.Pending)
            .Include(i => i.League!).ThenInclude(l => l.Owner)
            .Include(i => i.Inviter)
            .AsNoTracking()
            .ToListAsync();

    public async Task<DateTime?> GetLastDeclinedAtAsync(int leagueId, int inviteeId) =>
        await db.LeagueInvitations
            .Where(i => i.LeagueId == leagueId && i.InviteeId == inviteeId && i.Status == LeagueInvitationStatus.Declined)
            .MaxAsync(i => i.UpdatedAt);

    /// <inheritdoc />
    public async Task<LeagueInvitation> CreateInvitationAsync(LeagueInvitation invitation)
    {
        db.LeagueInvitations.Add(invitation);
        await SaveOrDetachOnFailureAsync(invitation);
        return invitation;
    }

    /// <inheritdoc />
    public async Task<bool> TryCloseInvitationAsync(int invitationId, LeagueInvitationStatus status, DateTime nowUtc) =>
        await TryMovePendingInvitationAsync(invitationId, status, nowUtc);

    /// <inheritdoc />
    /// <remarks>
    /// SQLite runs the transaction as BEGIN IMMEDIATE, so concurrent accepts are serialized and the second
    /// one finds the invitation no longer pending. Returning without commit rolls the claim back, so an
    /// invite link is not used up by someone who is already a member.
    /// </remarks>
    public async Task<AcceptInvitationResult> AcceptInvitationAsync(int invitationId, int userId, DateTime nowUtc)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        if (!await TryMovePendingInvitationAsync(invitationId, LeagueInvitationStatus.Accepted, nowUtc))
            return AcceptInvitationResult.NotPending;

        var leagueId = await GetLeagueIdAsync(invitationId);
        if (!await TryAddMemberAsync(leagueId, userId, nowUtc))
            return AcceptInvitationResult.AlreadyMember;

        await transaction.CommitAsync();
        return AcceptInvitationResult.Accepted;
    }

    private async Task<bool> TryMovePendingInvitationAsync(int invitationId, LeagueInvitationStatus status, DateTime nowUtc)
    {
        var updatedRows = await db.LeagueInvitations
            .Where(i => i.Id == invitationId && i.Status == LeagueInvitationStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Status, status)
                .SetProperty(i => i.UpdatedAt, (DateTime?)nowUtc));
        return updatedRows == 1;
    }

    private async Task<int> GetLeagueIdAsync(int invitationId) =>
        await db.LeagueInvitations
            .Where(i => i.Id == invitationId)
            .Select(i => i.LeagueId)
            .SingleAsync();

    private async Task<bool> TryAddMemberAsync(int leagueId, int userId, DateTime nowUtc)
    {
        var member = new LeagueMember
        {
            LeagueId = leagueId,
            UserId = userId,
            Role = LeagueRole.Member,
            JoinedAt = nowUtc
        };
        db.LeagueMembers.Add(member);
        try
        {
            await SaveOrDetachOnFailureAsync(member);
            return true;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return false;
        }
    }

    /// <summary>
    /// A failed insert stays tracked, so every later SaveChanges in this scope would retry (and fail on) it;
    /// detach it before rethrowing.
    /// </summary>
    private async Task SaveOrDetachOnFailureAsync(object insertedEntity)
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch
        {
            db.Entry(insertedEntity).State = EntityState.Detached;
            throw;
        }
    }
}
