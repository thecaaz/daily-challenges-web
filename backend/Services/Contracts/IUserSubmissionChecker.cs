using System.Security.Claims;
using DailyChallenges.Models;

namespace DailyChallenges.Services
{
    public interface IUserSubmissionChecker
    {
        Task<bool> HasUserSubmittedForLatestAsync(int gameId, ClaimsPrincipal? user);
        Task<bool> HasUserSubmittedForDayAsync(int? userId, int gameId, DateTime scoringDay, Game? game);
    }
}
