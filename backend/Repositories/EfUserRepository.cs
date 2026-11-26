using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

/// <summary>Username has NOCASE collation, so equality comparisons on it ignore case.</summary>
public sealed class EfUserRepository(AppDbContext db) : IUserRepository
{
    private const int DefaultPageSize = 50;

    /// <summary>Ordered by id. A page size below 1 reads as <see cref="DefaultPageSize"/>.</summary>
    public async Task<(List<User> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search)
    {
        var matching = MatchingSearch(db.Users.AsQueryable(), search);
        var total = await matching.CountAsync();
        var users = await matching
            .OrderBy(u => u.Id)
            .TakePage(page, pageSize.ValidPageSizeOr(DefaultPageSize))
            .ToListAsync();
        return (users, total);
    }

    public async Task<User?> GetByIdAsync(int userId) => await db.Users.FindAsync(userId);

    public Task<User?> GetByUsernameAsync(string username) =>
        db.Users.FirstOrDefaultAsync(u => u.Username == username);

    public Task<bool> ExistsWithUsernameAsync(string username) =>
        db.Users.AnyAsync(u => u.Username == username);

    public async Task<bool> IsEmptyAsync() => !await db.Users.AnyAsync();

    public async Task<User> CreateAsync(User user)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task<User> UpdateAsync(User user)
    {
        db.Users.Update(user);
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>Lower-cases both sides: instr() (what Contains translates to) ignores the column's NOCASE collation.</summary>
    private static IQueryable<User> MatchingSearch(IQueryable<User> users, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return users;

        var term = search.Trim().ToLower();
        return users.Where(u => u.Username.ToLower().Contains(term));
    }
}
