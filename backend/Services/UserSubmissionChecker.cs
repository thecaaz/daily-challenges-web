using System.Security.Claims;
using DailyChallenges.Repositories;
using DailyChallenges.Models;

namespace DailyChallenges.Services
{
    public class UserSubmissionChecker : IUserSubmissionChecker
    {
        private readonly ISubmissionRepository _subs;
        private readonly IGameRepository _games;

        public UserSubmissionChecker(ISubmissionRepository subs, IGameRepository games)
        {
            _subs = subs;
            _games = games;
        }

        public async Task<bool> HasUserSubmittedForLatestAsync(int gameId, ClaimsPrincipal? user)
        {
            var game = await _games.GetByIdAsync(gameId);
            var currentDay = ScoringDayHelper.GetCurrentScoringDay(game?.ResetTime ?? TimeSpan.Zero, game?.ResetTimezoneId ?? "UTC");
            int? userId = ClaimsPrincipalExtensions.GetUserId(user);
            return await HasUserSubmittedForDayAsync(userId, gameId, currentDay, game);
        }

        public async Task<bool> HasUserSubmittedForDayAsync(int? userId, int gameId, DateTime scoringDay, Game? game)
        {
            if (!userId.HasValue) return false;
            var latest = await _subs.GetByGameAndUserAsync(gameId, userId.Value);
            if (latest == null) return false;

            // Prefer stored ScoringDay when present (added by migration). Fallback to computing from CreatedAt.
            if (latest.ScoringDay != DateTime.MinValue)
                return latest.ScoringDay.Date == scoringDay.Date;

            var fallback = ScoringDayHelper.GetScoringDay(latest.CreatedAt, game?.ResetTime ?? TimeSpan.Zero, game?.ResetTimezoneId ?? "UTC");
            return fallback == scoringDay.Date;
        }
    }
}
