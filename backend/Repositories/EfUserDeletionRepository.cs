using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public sealed class EfUserDeletionRepository(AppDbContext db) : IUserDeletionRepository
{
    public Task<User?> GetUserAsync(int userId) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == userId);

    public Task<int> DeleteFriendRequestsAsync(int userId) =>
        db.FriendRequests
            .Where(f => f.SenderId == userId || f.ReceiverId == userId)
            .ExecuteDeleteAsync();

    public Task<int> DeleteLeagueInvitationsAsync(int userId) =>
        db.LeagueInvitations
            .Where(i => i.InviterId == userId || i.InviteeId == userId)
            .ExecuteDeleteAsync();

    public Task<List<League>> GetOwnedLeaguesAsync(int userId) =>
        db.Leagues.Where(l => l.OwnerId == userId).ToListAsync();

    public Task<LeagueMember?> GetLongestStandingOtherMemberAsync(int leagueId, int userId) =>
        db.LeagueMembers
            .Where(m => m.LeagueId == leagueId && m.UserId != userId)
            .OrderBy(m => m.JoinedAt).ThenBy(m => m.Id)
            .FirstOrDefaultAsync();

    public void RemoveLeague(League league) => db.Leagues.Remove(league);

    public Task AnonymizeSubmissionsAsync(int userId) =>
        db.Submissions
            .Where(s => s.UserId == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.Username, (string?)null));

    public async Task RemoveUserAsync(User user)
    {
        db.Users.Remove(user);
        await db.SaveChangesAsync();
    }
}
