using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public sealed class EfLeagueRepository(AppDbContext db) : ILeagueRepository
{
    public async Task<League?> GetByIdAsync(int leagueId, CancellationToken ct) =>
        await db.Leagues
            .Include(l => l.Owner)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == leagueId, ct);

    /// <inheritdoc />
    public async Task<League?> GetByIdWithMembersAsync(int leagueId, CancellationToken ct) =>
        await db.Leagues
            .Include(l => l.Owner)
            .Include(l => l.Members).ThenInclude(m => m.User)
            .Include(l => l.Invitations).ThenInclude(i => i.Invitee)
            .Include(l => l.Invitations).ThenInclude(i => i.Inviter)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == leagueId, ct);

    public async Task<List<League>> GetByUserAsync(int userId, CancellationToken ct) =>
        await db.LeagueMembers
            .Where(m => m.UserId == userId)
            .Include(m => m.League!).ThenInclude(l => l.Owner)
            .Include(m => m.League!).ThenInclude(l => l.Members)
            .AsNoTracking()
            .Select(m => m.League!)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<League> CreateAsync(League league, CancellationToken ct)
    {
        db.Leagues.Add(league);
        await db.SaveChangesAsync(ct);
        return league;
    }

    public async Task<bool> RenameAsync(int leagueId, string name, CancellationToken ct)
    {
        var league = await db.Leagues.FindAsync([leagueId], ct);
        if (league == null) return false;

        league.Name = name;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int leagueId, CancellationToken ct)
    {
        var league = await db.Leagues.FindAsync([leagueId], ct);
        if (league == null) return false;

        db.Leagues.Remove(league);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<LeagueMember?> GetMemberAsync(int leagueId, int userId, CancellationToken ct) =>
        await db.LeagueMembers.FirstOrDefaultAsync(m => m.LeagueId == leagueId && m.UserId == userId, ct);

    public async Task RemoveMemberAsync(LeagueMember member, CancellationToken ct)
    {
        db.LeagueMembers.Remove(member);
        await db.SaveChangesAsync(ct);
    }
}
