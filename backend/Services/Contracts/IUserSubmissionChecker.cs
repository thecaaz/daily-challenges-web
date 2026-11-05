using System.Security.Claims;

namespace DailyChallenges.Services
{
    public interface IUserSubmissionChecker
    {
        Task<bool> HasUserSubmittedForLatestAsync(int gameId, ClaimsPrincipal? user);
        /// <summary>True when the user has a submission stored for the given scoring day (an Any() query, no blob).</summary>
        Task<bool> HasUserSubmittedForDayAsync(int? userId, int gameId, DateTime scoringDay);
    }
}
