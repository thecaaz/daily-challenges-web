using DailyChallenges.Achievements;
using DailyChallenges.Data;
using DailyChallenges.Exceptions;
using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace DailyChallenges.Services
{
    public class AdminUserService : IAdminUserService
    {
        private readonly IUserRepository _userRepo;
        private readonly IXpService _xpService;
        private readonly LevelCalculator _levelCalc;
        private readonly IXpEventRepository _xpEventRepository;
        private readonly AppDbContext _db;
        private readonly IAchievementService _achievements;
        private readonly TimeProvider _time;
        private readonly ILogger<AdminUserService> _logger;

        public AdminUserService(IUserRepository userRepo, IXpService xpService, LevelCalculator levelCalc, IXpEventRepository xpEventRepository,
            AppDbContext db, IAchievementService achievements, TimeProvider time, ILogger<AdminUserService> logger)
        {
            _logger = logger;
            _userRepo = userRepo;
            _xpService = xpService;
            _levelCalc = levelCalc;
            _xpEventRepository = xpEventRepository;
            _db = db;
            _achievements = achievements;
            _time = time;
        }

        private DateTime TodayUtc => _time.GetUtcNow().UtcDateTime.Date;

        public async Task<(List<UserDto> Items, int TotalCount)> GetUsersAsync(int page = 1, int pageSize = 50, string? search = null)
        {
            var (users, total) = await _userRepo.GetPagedAsync(page, pageSize, search);
            var items = users.Select(u => DtoMapper.ToDto(u, _levelCalc, TodayUtc)).ToList();
            return (items, total);
        }

        public async Task<UserDto> AdjustXpAsync(int userId, int delta, string? reason, int? adminUserId = null)
        {
            // Read-modify-write of TotalXp inside BEGIN IMMEDIATE, so a concurrent submission or day win
            // cannot overwrite it. The user is first loaded inside the transaction.
            await using (var tx = await _db.Database.BeginTransactionAsync())
            {
                try
                {
                    // Throws KeyNotFoundException when the user does not exist.
                    await _xpService.AdjustXpAsync(userId, delta, reason ?? "admin_adjustment", adminUserId);
                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    _db.ChangeTracker.Clear();
                    throw;
                }
            }

            _logger.LogInformation("Admin {AdminId} adjusted XP of user {UserId} by {Delta} ({Reason})", adminUserId, userId, delta, reason ?? "admin_adjustment");

            await _achievements.CheckAndAwardAsync(userId, AchievementTrigger.LevelUp);

            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null) throw new KeyNotFoundException($"User {userId} not found");
            return DtoMapper.ToDto(user, _levelCalc, TodayUtc);
        }

        public async Task<(List<XpEventDto> Items, int TotalCount)> GetXpEventsAsync(int userId, int page = 1, int pageSize = 20, DateTime? from = null, DateTime? to = null, string? eventType = null)
        {
            var (items, total) = await _xpEventRepository.GetByUserPagedAsync(userId, page, pageSize, from, to, eventType);
            var dtos = items.Select(e => DtoMapper.ToDto(e)).ToList();
            return (dtos, total);
        }

        public async Task<(byte[] Data, string Filename)> ExportXpEventsCsvAsync(int userId, DateTime? from = null, DateTime? to = null, string? eventType = null, int maxRows = 10000)
        {
            var (items, total) = await GetXpEventsAsync(userId, 1, maxRows, from, to, eventType);

            var sb = new StringBuilder();
            sb.AppendLine("Id,UserId,SubmissionId,GameId,ScoringDay,Amount,EventType,Details,CreatedAt");

            string Escape(string? s) => EscapeCsvText(s);

            foreach (var it in items)
            {
                var scoringDay = it.ScoringDay ?? string.Empty;
                var createdAt = it.CreatedAt.ToString("o");
                var line = string.Join(',', new string[] {
                    it.Id.ToString(),
                    it.UserId.ToString(),
                    it.SubmissionId?.ToString() ?? string.Empty,
                    it.GameId?.ToString() ?? string.Empty,
                    scoringDay,
                    it.Amount.ToString(),
                    Escape(it.EventType),
                    Escape(it.Details),
                    Escape(createdAt)
                });
                sb.AppendLine(line);
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var filename = $"xp-events-user-{userId}.csv";
            return (bytes, filename);
        }

        /// <summary>
        /// Quotes a text cell for CSV. Cells starting with = + - @ (or tab / CR) get a leading apostrophe so
        /// spreadsheet apps don't evaluate them as formulas (CSV injection).
        /// </summary>
        public static string EscapeCsvText(string? s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            if (s[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
                s = "'" + s;
            return '"' + s.Replace("\"", "\"\"") + '"';
        }

        public async Task SetPasswordAsync(int userId, string newPassword, int? requestingAdminId = null)
        {
            CredentialRules.ValidatePassword(newPassword);

            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null) throw new KeyNotFoundException($"User {userId} not found");
            if (user.IsAdmin && userId != requestingAdminId)
                throw new ForbiddenException("Cannot reset another admin's password");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            // Ends every existing session of that user (refresh tokens carry the old version).
            user.TokenVersion++;
            await _userRepo.UpdateAsync(user);

            _logger.LogInformation("Admin {AdminId} reset the password of user {UserId}", requestingAdminId, userId);
        }

        public async Task DeleteUserAsync(int userId, int requestingAdminId)
        {
            if (userId == requestingAdminId) throw new ValidationException("Cannot delete your own account");

            // Everything below in one transaction: the Restrict FKs (friend request receiver, league owner,
            // invitation inviter) stay as a safety net, so every dependent row is handled before the delete.
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null) throw new KeyNotFoundException($"User {userId} not found");
                if (user.IsAdmin) throw new ForbiddenException("Cannot delete an admin account");

                var friendRequests = await _db.FriendRequests
                    .Where(f => f.SenderId == userId || f.ReceiverId == userId)
                    .ExecuteDeleteAsync();

                // Sent and received; received ones would otherwise stay Pending with InviteeId = null.
                var invitations = await _db.LeagueInvitations
                    .Where(i => i.InviterId == userId || i.InviteeId == userId)
                    .ExecuteDeleteAsync();

                // Owned leagues go to the longest-standing other member, or are deleted when nobody else is in them.
                var ownedLeagues = await _db.Leagues.Where(l => l.OwnerId == userId).ToListAsync();
                foreach (var league in ownedLeagues)
                {
                    var heir = await _db.LeagueMembers
                        .Where(m => m.LeagueId == league.Id && m.UserId != userId)
                        .OrderBy(m => m.JoinedAt).ThenBy(m => m.Id)
                        .FirstOrDefaultAsync();

                    if (heir != null)
                    {
                        league.OwnerId = heir.UserId;
                        heir.Role = LeagueRole.Owner;
                        _logger.LogInformation("League {LeagueId} transferred from deleted user {UserId} to user {NewOwnerId}", league.Id, userId, heir.UserId);
                    }
                    else
                    {
                        _db.Leagues.Remove(league);
                        _logger.LogInformation("League {LeagueId} deleted with its only member, user {UserId}", league.Id, userId);
                    }
                }

                // Submissions stay but are anonymized (UserId is set to null by the FK on delete).
                await _db.Submissions
                    .Where(s => s.UserId == userId)
                    .ExecuteUpdateAsync(set => set.SetProperty(s => s.Username, (string?)null));

                user.TokenVersion++;
                _db.Users.Remove(user);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                _logger.LogInformation(
                    "Admin {AdminId} deleted user {UserId} ({FriendRequests} friend requests, {Invitations} invitations, {Leagues} owned leagues handled)",
                    requestingAdminId, userId, friendRequests, invitations, ownedLeagues.Count);
            }
            catch
            {
                await tx.RollbackAsync();
                _db.ChangeTracker.Clear();
                throw;
            }
        }
    }
}
