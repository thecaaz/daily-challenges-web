using DailyChallenges.Models;

namespace DailyChallenges.Repositories.Contracts
{
    public interface IUserRepository
    {
        Task<(List<User> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search);
        Task<User?> GetByIdAsync(int userId);
        Task<User?> GetByUsernameAsync(string username);
        Task<bool> ExistsWithUsernameAsync(string username);
        Task<bool> IsEmptyAsync();
        Task<User> CreateAsync(User user);
        Task<User> UpdateAsync(User user);
        Task DeleteAsync(int userId);
    }
}
