using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Ranking;
using DailyChallenges.DTOs;
using System.Linq;
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

        public async Task<List<(int UserId, string Username, int Rank, string Score, double ScoreValue, int SubmissionId, bool HasScreenshot)>> GetLeaderboardAsync(
            int leagueId, int gameId, DateTime scoringDay, IRankingStrategy strategy)
        {
            var targetDay = scoringDay.Date;

            // Get member user IDs for this league
            var memberIds = await _db.LeagueMembers
                .Where(m => m.LeagueId == leagueId)
                .Select(m => m.UserId)
                .ToListAsync();

            if (memberIds.Count == 0)
                return new List<(int, string, int, string, double, int, bool)>();

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
                .Select(s => new {
                    s.Id,
                    s.UserId,
                    s.Score,
                    s.ScoreValue,
                    s.CreatedAt,
                    Username = s.Username != null ? s.Username : s.User != null ? s.User.Username : "Unknown",
                    HasScreenshot = s.ScreenshotData != null && s.ScreenshotData.Length > 0
                })
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
            var result = new List<(int UserId, string Username, int Rank, string Score, double ScoreValue, int SubmissionId, bool HasScreenshot)>();
            int rank = 1;
            for (int i = 0; i < reordered.Count; i++)
            {
                if (i > 0 && Math.Abs(reordered[i].ScoreValue!.Value - reordered[i - 1].ScoreValue!.Value) > 1e-9)
                    rank = i + 1;

                var s = reordered[i];
                result.Add((s.UserId!.Value, s.Username ?? "Unknown", rank, s.Score, s.ScoreValue!.Value, s.Id, s.HasScreenshot));
            }

            return result;
        }

        public async Task<(List<LeagueGameSummaryDto> Items, int TotalCount)> GetLeagueGameSummariesAsync(int leagueId, int requestingUserId, int days = 7, int page = 1, int pageSize = 20)
        {
            var memberIds = await _db.LeagueMembers
                .Where(m => m.LeagueId == leagueId)
                .Select(m => m.UserId)
                .ToListAsync();

            if (memberIds.Count == 0)
                return (new List<LeagueGameSummaryDto>(), 0);

            var since = DateTime.UtcNow.Date.AddDays(-(days - 1));

            // Project only the columns we need — avoids loading ScreenshotData blobs
            var subs = await _db.Submissions
                .Where(s => s.UserId.HasValue && memberIds.Contains(s.UserId!.Value)
                            && s.ScoringDay >= since.Date && s.ScoringDay <= DateTime.UtcNow.Date)
                .Select(s => new {
                    s.GameId,
                    GameName = s.Game!.Name,
                    GameHasIcon = s.Game.ScreenshotData != null,
                    GameRankingMode = s.Game!.RankingMode,
                    s.UserId,
                    s.Score,
                    s.ScoreValue,
                    s.ScoringDay,
                    s.CreatedAt,
                })
                .ToListAsync();

            var groups = subs.GroupBy(s => s.GameId);
            var results = new List<LeagueGameSummaryDto>();

            foreach (var g in groups)
            {
                var first = g.First();
                var isLowest = first.GameRankingMode == RankingMode.Lowest;
                var playCount = g.Count();
                var lastPlayedAt = g.Max(s => s.CreatedAt);

                var scored = g.Where(s => s.ScoreValue.HasValue).ToList();

                var top = scored.Count > 0
                    ? (isLowest
                        ? scored.OrderBy(s => s.ScoreValue).ThenBy(s => s.CreatedAt).First()
                        : scored.OrderByDescending(s => s.ScoreValue).ThenBy(s => s.CreatedAt).First())
                    : null;

                // Best submission per user
                var bestPerUser = scored
                    .GroupBy(s => s.UserId!.Value)
                    .Select(gr =>
                        isLowest
                            ? gr.OrderBy(s => s.ScoreValue).ThenBy(s => s.CreatedAt).First()
                            : gr.OrderByDescending(s => s.ScoreValue).ThenBy(s => s.CreatedAt).First())
                    .ToList();

                var sorted = isLowest
                    ? bestPerUser.OrderBy(s => s.ScoreValue).ThenBy(s => s.CreatedAt).ToList()
                    : bestPerUser.OrderByDescending(s => s.ScoreValue).ThenBy(s => s.CreatedAt).ToList();

                int? myRank = null;
                int rank = 1;
                for (int i = 0; i < sorted.Count; i++)
                {
                    if (i > 0 && Math.Abs(sorted[i].ScoreValue!.Value - sorted[i - 1].ScoreValue!.Value) > 1e-9)
                        rank = i + 1;

                    if (sorted[i].UserId == requestingUserId)
                    {
                        myRank = rank;
                        break;
                    }
                }

                var myBest = g.Where(s => s.UserId == requestingUserId && s.ScoreValue.HasValue).ToList();
                var myBestSub = myBest.Count > 0
                    ? (isLowest
                        ? myBest.OrderBy(s => s.ScoreValue).ThenBy(s => s.CreatedAt).First()
                        : myBest.OrderByDescending(s => s.ScoreValue).ThenBy(s => s.CreatedAt).First())
                    : null;

                // Recent plays histogram: one count per day, oldest→newest
                var recentPlays = new List<int>();
                for (int d = 0; d < days; d++)
                {
                    var day = since.AddDays(d).Date;
                    recentPlays.Add(g.Count(s => s.ScoringDay.Date == day));
                }

                results.Add(new LeagueGameSummaryDto
                {
                    GameId = first.GameId,
                    GameName = first.GameName,
                    IconUrl = first.GameHasIcon ? $"/api/games/{first.GameId}/image" : null,
                    LastPlayedAt = lastPlayedAt,
                    PlayCount = playCount,
                    TopScore = top?.Score,
                    TopScoreValue = top?.ScoreValue,
                    MyBestScore = myBestSub?.Score,
                    MyBestScoreValue = myBestSub?.ScoreValue,
                    MyRank = myRank,
                    RecentPlays = recentPlays,
                });
            }

            // Sort by last played descending, then paginate
            var ordered = results.OrderByDescending(r => r.LastPlayedAt).ToList();

            if (pageSize < 1) pageSize = 20;
            if (page < 1) page = 1;
            var total = ordered.Count;
            var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return (pageItems, total);
        }
    }
}
