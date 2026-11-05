using System.Security.Claims;
using DailyChallenges.Repositories;

namespace DailyChallenges.Services
{
    public class UserSubmissionChecker : IUserSubmissionChecker
    {
        private readonly TimeProvider _time;
        private readonly ISubmissionRepository _subs;
        private readonly IGameRepository _games;

        public UserSubmissionChecker(ISubmissionRepository subs, IGameRepository games, TimeProvider time)
        {
            _subs = subs;
            _games = games;
            _time = time;
        }

        public async Task<bool> HasUserSubmittedForLatestAsync(int gameId, ClaimsPrincipal? user)
        {
            int? userId = user.GetUserId();
            if (!userId.HasValue) return false;
            var game = await _games.GetByIdWithoutBlobAsync(gameId);
            var currentDay = ScoringDayHelper.GetCurrentScoringDay(game?.ResetTime ?? TimeSpan.Zero, _time.GetUtcNow().UtcDateTime);
            return await HasUserSubmittedForDayAsync(userId, gameId, currentDay);
        }

        public async Task<bool> HasUserSubmittedForDayAsync(int? userId, int gameId, DateTime scoringDay)
        {
            if (!userId.HasValue) return false;
            return await _subs.ExistsForUserGameDayAsync(userId.Value, gameId, scoringDay);
        }
    }
}
