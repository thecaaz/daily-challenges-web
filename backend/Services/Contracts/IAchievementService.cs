using DailyChallenges.Achievements;
using DailyChallenges.DTOs;

namespace DailyChallenges.Services.Contracts;

public interface IAchievementService
{
    /// <summary>
    /// Checks all achievements relevant to the given trigger for <paramref name="userId"/>,
    /// awards any newly earned ones, and creates notifications. Fire-and-forget safe — errors
    /// are logged but not rethrown so they never break the calling flow.
    /// Callers run it after committing their own work, so it takes no CancellationToken: a client abort
    /// must not leave a committed submission, day win or friendship without its achievements.
    /// </summary>
    Task CheckAndAwardAsync(int userId, AchievementTrigger trigger);

    /// <summary>
    /// Returns the full catalog merged with the user's unlock state.
    /// Locked achievements have a null <c>UnlockedAt</c>.
    /// </summary>
    Task<List<AchievementDto>> GetForUserAsync(int userId, CancellationToken ct);

    /// <summary>The full catalog without any unlock state, in catalog order.</summary>
    IReadOnlyList<AchievementCatalogEntryDto> GetCatalog();
}
