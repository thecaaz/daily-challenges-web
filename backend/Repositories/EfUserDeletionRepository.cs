using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public sealed class EfUserDeletionRepository(AppDbContext db) : IUserDeletionRepository
{
    public Task<User?> GetUserAsync(int userId, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

    public Task<int> DeleteFriendRequestsAsync(int userId, CancellationToken ct) =>
        db.FriendRequests
            .Where(f => f.SenderId == userId || f.ReceiverId == userId)
            .ExecuteDeleteAsync(ct);

    public Task<int> DeleteLeagueInvitationsAsync(int userId, CancellationToken ct) =>
        db.LeagueInvitations
            .Where(i => i.InviterId == userId || i.InviteeId == userId)
            .ExecuteDeleteAsync(ct);

    public Task<List<League>> GetOwnedLeaguesAsync(int userId, CancellationToken ct) =>
        db.Leagues.Where(l => l.OwnerId == userId).ToListAsync(ct);

    public Task<LeagueMember?> GetLongestStandingOtherMemberAsync(int leagueId, int userId, CancellationToken ct) =>
        db.LeagueMembers
            .Where(m => m.LeagueId == leagueId && m.UserId != userId)
            .OrderBy(m => m.JoinedAt).ThenBy(m => m.Id)
            .FirstOrDefaultAsync(ct);

    public void RemoveLeague(League league) => db.Leagues.Remove(league);

    public Task AnonymizeSubmissionsAsync(int userId, CancellationToken ct) =>
        db.Submissions
            .Where(s => s.UserId == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.Username, (string?)null), ct);

    public async Task RemoveUserAsync(User user, CancellationToken ct)
    {
        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
    }
}
