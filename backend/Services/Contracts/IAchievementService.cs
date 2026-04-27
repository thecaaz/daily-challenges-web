using DailyChallenges.Achievements;
using DailyChallenges.DTOs;

namespace DailyChallenges.Services.Contracts
{
    public interface IAchievementService
    {
        /// <summary>
        /// Checks all achievements relevant to the given trigger for <paramref name="userId"/>,
        /// awards any newly earned ones, and creates notifications. Fire-and-forget safe — errors
        /// are logged but not rethrown so they never break the calling flow.
        /// </summary>
        Task CheckAndAwardAsync(int userId, AchievementTrigger trigger);

        /// <summary>
        /// Returns the full catalog merged with the user's unlock state.
        /// Locked achievements have a null <c>UnlockedAt</c>.
        /// </summary>
        Task<List<AchievementDto>> GetForUserAsync(int userId);
    }
}
