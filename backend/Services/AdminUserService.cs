using Microsoft.EntityFrameworkCore;
using DailyChallenges.Data;
using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
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

        public async Task<(List<DailyChallenges.DTOs.XpEventDto> Items, int TotalCount)> GetXpEventsAsync(int userId, int page = 1, int pageSize = 20, DateTime? from = null, DateTime? to = null, string? eventType = null)
        {
            var (items, total) = await _xpEventRepository.GetByUserPagedAsync(userId, page, pageSize, from, to, eventType);
            var dtos = items.Select(e => DtoMapper.ToDto(e)).ToList();
            return (dtos, total);
        }
    }
}
