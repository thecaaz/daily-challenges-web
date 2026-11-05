using System.Security.Claims;
using DailyChallenges.Models;

namespace DailyChallenges.Services
{
    /// <summary>
    /// The hidden-scores rule: scores of a game's current scoring day are visible only to admins
    /// and to users who have submitted for that day. Past days are always visible.
    /// </summary>
    public interface IScoreVisibilityService
    {
        /// <summary>The game's current scoring day (date only), from the injected clock.</summary>
        DateTime GetCurrentScoringDay(Game game);

        /// <summary>True when <paramref name="scoringDay"/> is the game's current scoring day.</summary>
        bool IsCurrentDay(Game game, DateTime scoringDay);

        Task<bool> CanSeeDayAsync(ClaimsPrincipal? user, Game game, DateTime scoringDay);

        /// <summary>Same rule for callers that already resolved the user id and admin flag.</summary>
        Task<bool> CanSeeDayAsync(int? userId, bool isAdmin, Game game, DateTime scoringDay);
    }
}
