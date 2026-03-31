using DailyChallenges.Models;

namespace DailyChallenges.Repositories
{
    public interface ISubmissionRepository
    {
        Task<List<Submission>> GetByGameAsync(int gameId);
        Task<List<Submission>> GetByGamePagedAsync(int gameId, int page, int pageSize);
        Task<List<Submission>> GetTopByGameAsync(int gameId, int top);
        Task<List<Submission>> GetTopByGameByScoreValueAsync(int gameId, int top);
        Task<List<Submission>> GetByGameAndDayByScoreValueAsync(int gameId, DateTime scoringDay);
        Task<Submission?> GetByGameAndUserAsync(int gameId, int userId);
        Task<(List<Submission> Items, int TotalCount, List<DateTime> AvailableDates)> GetByGameFilteredAsync(int gameId, int page, int pageSize, string? search, DateTime? scoringDay, DateTime? excludeScoringDay = null);
        Task<Submission?> GetWinnerForGameAndDayAsync(int gameId, DateTime scoringDay);
        Task<List<Submission>> GetWinnersForGameAndDaysAsync(int gameId, List<DateTime> days);
        Task<List<DateTime>> GetAvailableDatesAsync(int gameId);
        Task<List<string>> GetUsernamesForDayAsync(int gameId, DateTime scoringDay);
        Task<Submission> CreateAsync(Submission submission);
        Task<Submission?> GetByIdAsync(int id);
        Task<Submission> UpdateAsync(Submission submission);
        Task DeleteAsync(int id);
    }
}
