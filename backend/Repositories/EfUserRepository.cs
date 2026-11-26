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
    public async Task<(List<User> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search, CancellationToken ct)
    {
        var matching = MatchingSearch(db.Users.AsQueryable(), search);
        var total = await matching.CountAsync(ct);
        var users = await matching
            .OrderBy(u => u.Id)
            .TakePage(page, pageSize.ValidPageSizeOr(DefaultPageSize))
            .ToListAsync(ct);
        return (users, total);
    }

    public async Task<User?> GetByIdAsync(int userId, CancellationToken ct) => await db.Users.FindAsync([userId], ct);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);

    public Task<bool> ExistsWithUsernameAsync(string username, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Username == username, ct);

    public async Task<bool> IsEmptyAsync(CancellationToken ct) => !await db.Users.AnyAsync(ct);

    public async Task<User> CreateAsync(User user, CancellationToken ct)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<User> UpdateAsync(User user, CancellationToken ct)
    {
        db.Users.Update(user);
        await db.SaveChangesAsync(ct);
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
