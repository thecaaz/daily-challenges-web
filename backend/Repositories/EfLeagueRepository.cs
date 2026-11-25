using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public sealed class EfLeagueRepository(AppDbContext db) : ILeagueRepository
{
    public async Task<League?> GetByIdAsync(int leagueId) =>
        await db.Leagues
            .Include(l => l.Owner)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == leagueId);

    /// <inheritdoc />
    public async Task<League?> GetByIdWithMembersAsync(int leagueId) =>
        await db.Leagues
            .Include(l => l.Owner)
            .Include(l => l.Members).ThenInclude(m => m.User)
            .Include(l => l.Invitations).ThenInclude(i => i.Invitee)
            .Include(l => l.Invitations).ThenInclude(i => i.Inviter)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == leagueId);

    public async Task<List<League>> GetByUserAsync(int userId) =>
        await db.LeagueMembers
            .Where(m => m.UserId == userId)
            .Include(m => m.League!).ThenInclude(l => l.Owner)
            .Include(m => m.League!).ThenInclude(l => l.Members)
            .AsNoTracking()
            .Select(m => m.League!)
            .ToListAsync();

    /// <inheritdoc />
    public async Task<League> CreateAsync(League league)
    {
        db.Leagues.Add(league);
        await db.SaveChangesAsync();
        return league;
    }

    public async Task<bool> RenameAsync(int leagueId, string name)
    {
        var league = await db.Leagues.FindAsync(leagueId);
        if (league == null) return false;

        league.Name = name;
        await db.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int leagueId)
    {
        var league = await db.Leagues.FindAsync(leagueId);
        if (league == null) return false;

        db.Leagues.Remove(league);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<LeagueMember?> GetMemberAsync(int leagueId, int userId) =>
        await db.LeagueMembers.FirstOrDefaultAsync(m => m.LeagueId == leagueId && m.UserId == userId);

    public async Task RemoveMemberAsync(LeagueMember member)
    {
        db.LeagueMembers.Remove(member);
        await db.SaveChangesAsync();
    }
}
