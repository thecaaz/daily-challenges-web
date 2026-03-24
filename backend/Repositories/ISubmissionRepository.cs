using DailyChallenges.Models;
namespace DailyChallenges.Repositories
{
    public interface ISubmissionRepository
    {
        Task<List<Submission>> GetByGameAsync(int gameId);
        Task<List<Submission>> GetByGamePagedAsync(int gameId, int page, int pageSize);
        Task<List<Submission>> GetTopByGameAsync(int gameId, int top);
        Task<Submission?> GetByGameAndUserAsync(int gameId, int userId);
        Task<Submission> CreateAsync(Submission submission);
        Task<Submission?> GetByIdAsync(int id);
        Task<Submission> UpdateAsync(Submission submission);
        Task DeleteAsync(int id);
    }
}
