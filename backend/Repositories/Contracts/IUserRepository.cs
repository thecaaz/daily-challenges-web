using DailyChallenges.Models;

namespace DailyChallenges.Repositories.Contracts;

public interface IUserRepository
{
    Task<(List<User> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search, CancellationToken ct);
    Task<User?> GetByIdAsync(int userId, CancellationToken ct);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct);
    Task<bool> ExistsWithUsernameAsync(string username, CancellationToken ct);
    Task<bool> IsEmptyAsync(CancellationToken ct);
    Task<User> CreateAsync(User user, CancellationToken ct);
    Task<User> UpdateAsync(User user, CancellationToken ct);
}
