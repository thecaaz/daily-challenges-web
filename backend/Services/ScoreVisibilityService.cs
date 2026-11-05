using System.Security.Claims;
using DailyChallenges.Models;

namespace DailyChallenges.Services
{
    public class ScoreVisibilityService : IScoreVisibilityService
    {
        private readonly IUserSubmissionChecker _checker;
        private readonly TimeProvider _time;

        public ScoreVisibilityService(IUserSubmissionChecker checker, TimeProvider time)
        {
            _checker = checker;
            _time = time;
        }

        public DateTime GetCurrentScoringDay(Game game) =>
            ScoringDayHelper.GetCurrentScoringDay(game.ResetTime, _time.GetUtcNow().UtcDateTime).Date;

        public bool IsCurrentDay(Game game, DateTime scoringDay) => scoringDay.Date == GetCurrentScoringDay(game);

        public Task<bool> CanSeeDayAsync(ClaimsPrincipal? user, Game game, DateTime scoringDay) =>
            CanSeeDayAsync(user.GetUserId(), user.IsAdmin(), game, scoringDay);

        public async Task<bool> CanSeeDayAsync(int? userId, bool isAdmin, Game game, DateTime scoringDay)
        {
            if (!IsCurrentDay(game, scoringDay)) return true;
            if (isAdmin) return true;
            return await _checker.HasUserSubmittedForDayAsync(userId, game.Id, scoringDay.Date);
        }
    }
}
