using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Ranking;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories
{
    public class EfLeagueRepository : ILeagueRepository
    {
        private readonly AppDbContext _db;
        public EfLeagueRepository(AppDbContext db) => _db = db;

        // ── Leagues ───────────────────────────────────────────────────────────

        public async Task<League?> GetByIdAsync(int leagueId)
        {
            return await _db.Leagues
                .Include(l => l.Owner)
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == leagueId);
        }

        public async Task<League?> GetByIdWithMembersAsync(int leagueId)
        {
            return await _db.Leagues
                .Include(l => l.Owner)
                .Include(l => l.Members).ThenInclude(m => m.User)
                .Include(l => l.Invitations).ThenInclude(i => i.Invitee)
                .Include(l => l.Invitations).ThenInclude(i => i.Inviter)
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == leagueId);
        }

        public async Task<List<League>> GetByUserAsync(int userId)
        {
            return await _db.LeagueMembers
                .Where(m => m.UserId == userId)
                .Include(m => m.League!).ThenInclude(l => l.Owner)
                .Include(m => m.League!).ThenInclude(l => l.Members)
                .AsNoTracking()
                .Select(m => m.League!)
                .ToListAsync();
        }

        public async Task<League> CreateAsync(League league)
        {
            _db.Leagues.Add(league);
            await _db.SaveChangesAsync();
            return league;
        }

        public async Task UpdateAsync(League league)
        {
            _db.Leagues.Update(league);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(League league)
        {
            _db.Leagues.Remove(league);
            await _db.SaveChangesAsync();
        }

        // ── Members ───────────────────────────────────────────────────────────

        public async Task<LeagueMember?> GetMemberAsync(int leagueId, int userId)
        {
            return await _db.LeagueMembers
                .FirstOrDefaultAsync(m => m.LeagueId == leagueId && m.UserId == userId);
        }

        public async Task<List<LeagueMember>> GetMembersAsync(int leagueId)
        {
            return await _db.LeagueMembers
                .Where(m => m.LeagueId == leagueId)
                .Include(m => m.User)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<LeagueMember> AddMemberAsync(LeagueMember member)
        {
            _db.LeagueMembers.Add(member);
            await _db.SaveChangesAsync();
            return member;
        }

        public async Task RemoveMemberAsync(LeagueMember member)
        {
            _db.LeagueMembers.Remove(member);
            await _db.SaveChangesAsync();
        }

        // ── Invitations ───────────────────────────────────────────────────────

        public async Task<LeagueInvitation?> GetInvitationByIdAsync(int invitationId)
        {
            return await _db.LeagueInvitations
                .Include(i => i.League)
                .Include(i => i.Inviter)
                .Include(i => i.Invitee)
                .FirstOrDefaultAsync(i => i.Id == invitationId);
        }

        public async Task<LeagueInvitation?> GetPendingInvitationAsync(int leagueId, int inviteeId)
        {
            return await _db.LeagueInvitations
                .FirstOrDefaultAsync(i =>
                    i.LeagueId == leagueId &&
                    i.InviteeId == inviteeId &&
                    i.Status == LeagueInvitationStatus.Pending);
        }

        public async Task<LeagueInvitation?> GetInvitationByTokenAsync(string token)
        {
            return await _db.LeagueInvitations
                .Include(i => i.League)
                .Include(i => i.Inviter)
                .FirstOrDefaultAsync(i => i.Token == token && i.Status == LeagueInvitationStatus.Pending);
        }

        public async Task<List<LeagueInvitation>> GetPendingInvitationsForUserAsync(int userId)
        {
            return await _db.LeagueInvitations
                .Where(i => i.InviteeId == userId && i.Status == LeagueInvitationStatus.Pending)
                .Include(i => i.League!).ThenInclude(l => l.Owner)
                .Include(i => i.Inviter)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<LeagueInvitation>> GetInvitationsForLeagueAsync(int leagueId)
        {
            return await _db.LeagueInvitations
                .Where(i => i.LeagueId == leagueId && i.Status == LeagueInvitationStatus.Pending)
                .Include(i => i.Invitee)
                .Include(i => i.Inviter)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<LeagueInvitation> CreateInvitationAsync(LeagueInvitation invitation)
        {
            _db.LeagueInvitations.Add(invitation);
            await _db.SaveChangesAsync();
            return invitation;
        }

        public async Task UpdateInvitationAsync(LeagueInvitation invitation)
        {
            _db.LeagueInvitations.Update(invitation);
            await _db.SaveChangesAsync();
        }

        // ── Leaderboard ───────────────────────────────────────────────────────

        public async Task<List<(int UserId, string Username, int Rank, string Score, int ScoreValue)>> GetLeaderboardAsync(
            int leagueId, int gameId, DateTime scoringDay, IRankingStrategy strategy)
        {
            var targetDay = scoringDay.Date;

            // Get member user IDs for this league
            var memberIds = await _db.LeagueMembers
                .Where(m => m.LeagueId == leagueId)
                .Select(m => m.UserId)
                .ToListAsync();

            if (memberIds.Count == 0)
                return new List<(int, string, int, string, int)>();

            // Get the best submission per user: apply strategy ordering, then take first per user
            var baseQuery = _db.Submissions
                .Where(s =>
                    s.GameId == gameId &&
                    s.ScoringDay == targetDay &&
                    s.UserId.HasValue &&
                    memberIds.Contains(s.UserId!.Value) &&
                    s.ScoreValue.HasValue);

            var ordered = strategy.ApplyOrdering(baseQuery);

            var submissions = await ordered
                .Include(s => s.User)
                .AsNoTracking()
                .ToListAsync();

            // Take the best submission per user (strategy ordering already sorted best-first)
            var bestPerUser = submissions
                .GroupBy(s => s.UserId!.Value)
                .Select(g => g.First())
                .ToList();

            // Re-order the grouped results using ScoreValue
            var reordered = strategy is Services.Ranking.LowestRankingStrategy
                ? bestPerUser.OrderBy(s => s.ScoreValue!.Value).ThenBy(s => s.CreatedAt).ToList()
                : bestPerUser.OrderByDescending(s => s.ScoreValue!.Value).ThenBy(s => s.CreatedAt).ToList();

            // Assign dense ranks (ties get the same rank)
            var result = new List<(int UserId, string Username, int Rank, string Score, int ScoreValue)>();
            int rank = 1;
            for (int i = 0; i < reordered.Count; i++)
            {
                if (i > 0 && reordered[i].ScoreValue != reordered[i - 1].ScoreValue)
                    rank = i + 1;

                var s = reordered[i];
                result.Add((s.UserId!.Value, s.Username ?? s.User?.Username ?? "Unknown", rank, s.Score, s.ScoreValue!.Value));
            }

            return result;
        }
    }
}
