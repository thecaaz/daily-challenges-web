using DailyChallenges.Models;

namespace DailyChallenges.Repositories
{
    public interface IScoringDayResultRepository
    {
        Task<bool> ExistsAsync(int gameId, DateTime scoringDay);
        Task<ScoringDayResult> CreateAsync(ScoringDayResult result);
    }
}
