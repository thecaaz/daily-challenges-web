using DailyChallenges.Models;
using DailyChallenges.Services;

namespace DailyChallenges.Mapping;

/// <summary>A user's level, XP into and to the next level, and effective streak, as shown in user DTOs.</summary>
public sealed record UserProgression(int Level, long XpIntoLevel, long XpToNextLevel, int Streak)
{
    /// <param name="todayUtc">From the caller's TimeProvider; decides the effective streak.</param>
    public static UserProgression Of(User user, LevelCalculator levels, DateTime todayUtc)
    {
        var (level, xpIntoLevel, xpToNextLevel) = levels.GetLevelProgress(user.TotalXp);
        return new UserProgression(level, xpIntoLevel, xpToNextLevel, StreakCalculator.GetEffectiveStreak(user, todayUtc));
    }
}
