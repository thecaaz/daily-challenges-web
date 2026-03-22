using DailyChallenges.Models;
namespace DailyChallenges.Repositories
{
    public interface ISubmissionRepository
    {
        Task<List<Submission>> GetByGameAsync(int gameId);
        Task<Submission> CreateAsync(Submission submission);
    }
}
