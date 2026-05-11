using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories
{
    public class EfUserRepository : IUserRepository
    {
        private readonly AppDbContext _db;
        public EfUserRepository(AppDbContext db) => _db = db;

        public async Task<(List<User> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;

            var q = _db.Users.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(u => u.Username.Contains(search));

            var total = await q.CountAsync();
            var users = await q.OrderBy(u => u.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (users, total);
        }

        public async Task<User?> GetByIdAsync(int userId)
        {
            return await _db.Users.FindAsync(userId);
        }

        public async Task<User?> GetByUsernameAsync(string username) =>
            await _db.Users.FirstOrDefaultAsync(u => u.Username == username);

        public Task<bool> ExistsWithUsernameAsync(string username) =>
            _db.Users.AnyAsync(u => u.Username == username);

        public Task<bool> IsEmptyAsync() =>
            _db.Users.AnyAsync().ContinueWith(t => !t.Result);

        public async Task<User> CreateAsync(User user)
        {
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return user;
        }

        public async Task<User> UpdateAsync(User user)
        {
            _db.Users.Update(user);
            await _db.SaveChangesAsync();
            return user;
        }

        public async Task DeleteAsync(int userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return;
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
        }
    }
}
