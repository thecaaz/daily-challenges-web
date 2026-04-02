using Microsoft.EntityFrameworkCore;
using DailyChallenges.Data;
using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using System.Text;
using DailyChallenges.Repositories;

namespace DailyChallenges.Services
{
    public class AdminUserService : IAdminUserService
    {
        private readonly AppDbContext _db;
        private readonly IXpService _xpService;
        private readonly LevelCalculator _levelCalc;
        private readonly IXpEventRepository _xpEventRepository;

        public AdminUserService(AppDbContext db, IXpService xpService, LevelCalculator levelCalc, IXpEventRepository xpEventRepository)
        {
            _db = db;
            _xpService = xpService;
            _levelCalc = levelCalc;
            _xpEventRepository = xpEventRepository;
        }

        public async Task<(List<UserDto> Items, int TotalCount)> GetUsersAsync(int page = 1, int pageSize = 50, string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;

            var q = _db.Users.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search)) q = q.Where(u => u.Username.Contains(search));

            var total = await q.CountAsync();
            var users = await q.OrderBy(u => u.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var items = users.Select(u => DtoMapper.ToDto(u, _levelCalc)).ToList();
            return (items, total);
        }

        public async Task<UserDto> AdjustXpAsync(int userId, int delta, string? reason, int? adminUserId = null)
        {
            // IXpService will throw KeyNotFoundException if user not found
            await _xpService.AdjustXpAsync(userId, delta, reason ?? "admin_adjustment", adminUserId);
            var user = await _db.Users.FindAsync(userId);
            if (user == null) throw new KeyNotFoundException($"User {userId} not found");
            return DtoMapper.ToDto(user, _levelCalc);
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

            string Escape(string? s)
            {
                if (string.IsNullOrEmpty(s)) return string.Empty;
                return '"' + s.Replace("\"", "\"\"") + '"';
            }

            foreach (var it in items)
            {
                var scoringDay = it.ScoringDay.HasValue ? ScoringDayHelper.FormatScoringDay(it.ScoringDay.Value) : string.Empty;
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

        public async Task SetPasswordAsync(int userId, string newPassword)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) throw new KeyNotFoundException($"User {userId} not found");
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteUserAsync(int userId, int requestingAdminId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) throw new KeyNotFoundException($"User {userId} not found");
            if (user.IsAdmin) throw new InvalidOperationException("Cannot delete an admin account");
            if (userId == requestingAdminId) throw new InvalidOperationException("Cannot delete your own account");
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
        }
    }
}
