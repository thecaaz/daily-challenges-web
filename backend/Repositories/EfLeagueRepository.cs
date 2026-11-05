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
        private readonly TimeProvider _time;

        public EfLeagueRepository(AppDbContext db, TimeProvider? time = null)
        {
            _db = db;
            _time = time ?? TimeProvider.System;
        }

        // ── Leagues ───────────────────────────────────────────────────────────

        public async Task<League?> GetByIdAsync(int leagueId)
        {
            return await _db.Leagues
                .Include(l => l.Owner)
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == leagueId);
        }

        /// <inheritdoc />
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
            // The league and its Members are inserted by one SaveChanges, which EF wraps in a transaction.
            _db.Leagues.Add(league);
            await _db.SaveChangesAsync();
            return league;
        }

        public async Task<bool> RenameAsync(int leagueId, string name)
        {
            var league = await _db.Leagues.FindAsync(leagueId);
            if (league == null) return false;
            league.Name = name;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int leagueId)
        {
            var league = await _db.Leagues.FindAsync(leagueId);
            if (league == null) return false;
            // Members and invitations are deleted by the database (ON DELETE CASCADE).
            _db.Leagues.Remove(league);
            await _db.SaveChangesAsync();
            return true;
        }

        // ── Members ───────────────────────────────────────────────────────────

        public async Task<LeagueMember?> GetMemberAsync(int leagueId, int userId)
        {
            return await _db.LeagueMembers
                .FirstOrDefaultAsync(m => m.LeagueId == leagueId && m.UserId == userId);
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
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == invitationId);
        }

        public async Task<LeagueInvitation?> GetPendingInvitationAsync(int leagueId, int inviteeId)
        {
            return await _db.LeagueInvitations
                .AsNoTracking()
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
                .AsNoTracking()
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

        public async Task<DateTime?> GetLastDeclinedAtAsync(int leagueId, int inviteeId)
        {
            return await _db.LeagueInvitations
                .Where(i => i.LeagueId == leagueId && i.InviteeId == inviteeId && i.Status == LeagueInvitationStatus.Declined)
                .MaxAsync(i => i.UpdatedAt);
        }

        public async Task<LeagueInvitation> CreateInvitationAsync(LeagueInvitation invitation)
        {
            _db.LeagueInvitations.Add(invitation);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch
            {
                // Detach the failed insert, or every later SaveChanges in this scope retries (and fails on) it.
                _db.Entry(invitation).State = EntityState.Detached;
                throw;
            }
            return invitation;
        }

        public async Task<bool> TryCloseInvitationAsync(int invitationId, LeagueInvitationStatus status, DateTime nowUtc)
        {
            var rows = await _db.LeagueInvitations
                .Where(i => i.Id == invitationId && i.Status == LeagueInvitationStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.Status, status)
                    .SetProperty(i => i.UpdatedAt, (DateTime?)nowUtc));
            return rows == 1;
        }

        public async Task<AcceptInvitationResult> AcceptInvitationAsync(int invitationId, int userId, DateTime nowUtc)
        {
            // BEGIN IMMEDIATE (SQLite): concurrent accepts are serialized; the second one finds the
            // invitation no longer pending.
            await using var tx = await _db.Database.BeginTransactionAsync();

            var claimed = await _db.LeagueInvitations
                .Where(i => i.Id == invitationId && i.Status == LeagueInvitationStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.Status, LeagueInvitationStatus.Accepted)
                    .SetProperty(i => i.UpdatedAt, (DateTime?)nowUtc));
            if (claimed == 0)
                return AcceptInvitationResult.NotPending; // disposing the transaction rolls it back

            var leagueId = await _db.LeagueInvitations
                .Where(i => i.Id == invitationId)
                .Select(i => i.LeagueId)
                .SingleAsync();

            var member = new LeagueMember
            {
                LeagueId = leagueId,
                UserId = userId,
                Role = LeagueRole.Member,
                JoinedAt = nowUtc
            };
            _db.LeagueMembers.Add(member);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                // Already a member: roll back the claim too, so an invite link is not used up.
                _db.Entry(member).State = EntityState.Detached;
                return AcceptInvitationResult.AlreadyMember;
            }
            catch
            {
                _db.Entry(member).State = EntityState.Detached;
                throw;
            }

            await tx.CommitAsync();
            return AcceptInvitationResult.Accepted;
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
                    HasScreenshot = s.ScreenshotContentType != null && s.ScreenshotContentType != ""
                })
                .ToListAsync();

            // Best submission per user. The rows are already in strategy order (best first), and GroupBy keeps
            // the order in which keys first appear, so the groups (and their first rows) stay in ranking order.
            var reordered = submissions
                .GroupBy(s => s.UserId!.Value)
                .Select(g => g.First())
                .ToList();

            // Standard competition ranks: equal scores share a rank and the next rank skips (1, 1, 3).
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

            var nowUtc = _time.GetUtcNow().UtcDateTime;
            // The window is the last `days` scoring days of each game, ending at the game's current scoring
            // day. A game's current day is within ±1 of the UTC date (late resets label "today" with tomorrow's
            // date), so query the widest possible range here and narrow it per game below.
            var rangeStart = nowUtc.Date.AddDays(-1 - (days - 1));
            var rangeEnd = nowUtc.Date.AddDays(1);

            // Project only the columns we need — avoids loading ScreenshotData blobs
            var rows = await _db.Submissions
                .Where(s => s.UserId.HasValue && memberIds.Contains(s.UserId!.Value)
                            && s.ScoringDay >= rangeStart && s.ScoringDay <= rangeEnd)
                .Select(s => new {
                    s.GameId,
                    GameName = s.Game!.Name,
                    GameHasIcon = s.Game.ScreenshotContentType != null && s.Game.ScreenshotContentType != "",
                    GameRankingMode = s.Game!.RankingMode,
                    GameResetTime = s.Game!.ResetTime,
                    s.UserId,
                    s.Score,
                    s.ScoreValue,
                    s.ScoringDay,
                    s.CreatedAt,
                })
                .ToListAsync();

            var currentDayByGame = rows
                .GroupBy(s => s.GameId)
                .ToDictionary(g => g.Key, g => Services.ScoringDayHelper.GetCurrentScoringDay(g.First().GameResetTime, nowUtc).Date);
            var subs = rows
                .Where(s => s.ScoringDay.Date <= currentDayByGame[s.GameId]
                         && s.ScoringDay.Date > currentDayByGame[s.GameId].AddDays(-days))
                .ToList();
            foreach (var gameId in currentDayByGame.Keys.ToList())
                if (!subs.Any(s => s.GameId == gameId)) currentDayByGame.Remove(gameId);

            // Hidden-scores rule: for games the requester has not submitted for on the game's current
            // scoring day, other members' current-day rows do not count towards TopScore / MyRank.
            var currentDays = currentDayByGame.Values.Distinct().ToList();
            var gameIds = currentDayByGame.Keys.ToList();
            var requesterCurrentRows = currentDays.Count == 0
                ? new List<(int GameId, DateTime ScoringDay)>()
                : (await _db.Submissions
                    .Where(s => s.UserId == requestingUserId && gameIds.Contains(s.GameId) && currentDays.Contains(s.ScoringDay))
                    .Select(s => new { s.GameId, s.ScoringDay })
                    .ToListAsync())
                    .Select(x => (x.GameId, x.ScoringDay))
                    .ToList();
            var submittedToday = requesterCurrentRows
                .Where(x => currentDayByGame[x.GameId] == x.ScoringDay.Date)
                .Select(x => x.GameId)
                .ToHashSet();

            var groups = subs.GroupBy(s => s.GameId);
            var results = new List<LeagueGameSummaryDto>();

            foreach (var g in groups)
            {
                var first = g.First();
                var isLowest = first.GameRankingMode == RankingMode.Lowest;
                var playCount = g.Count();
                var lastPlayedAt = g.Max(s => s.CreatedAt);
                var hideCurrentDay = !submittedToday.Contains(g.Key);
                var currentDay = currentDayByGame[g.Key];

                var scored = g
                    .Where(s => s.ScoreValue.HasValue)
                    .Where(s => !hideCurrentDay || s.UserId == requestingUserId || s.ScoringDay.Date != currentDay)
                    .ToList();

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

                // Recent plays histogram: one count per scoring day of this game, oldest→newest
                var since = currentDay.AddDays(-(days - 1));
                var recentPlays = new List<int>();
                var recentPlayDays = new List<string>();
                for (int d = 0; d < days; d++)
                {
                    var day = since.AddDays(d).Date;
                    recentPlays.Add(g.Count(s => s.ScoringDay.Date == day));
                    recentPlayDays.Add(Services.ScoringDayHelper.FormatScoringDay(day));
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
                    RecentPlayDays = recentPlayDays,
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
