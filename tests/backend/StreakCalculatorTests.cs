using DailyChallenges.Models;
using DailyChallenges.Services;

namespace DailyChallenges.Tests;

public class StreakCalculatorTests
{
    private static readonly DateTime Today = new(2026, 4, 10, 9, 0, 0, DateTimeKind.Utc);

    private static User UserWith(int streak, DateTime? lastSubmissionAt) =>
        new() { Id = 1, Username = "u", Streak = streak, LastSubmissionAt = lastSubmissionAt };

    [Theory]
    [InlineData(0, 4)]
    [InlineData(-1, 4)]
    [InlineData(-2, 0)]
    public void GetEffectiveStreak_IsZeroOnceADayWasMissed(int lastPlayedDaysAgo, int expected)
    {
        var user = UserWith(4, Today.Date.AddDays(lastPlayedDaysAgo).AddHours(23));

        Assert.Equal(expected, StreakCalculator.GetEffectiveStreak(user, Today));
    }

    [Fact]
    public void GetEffectiveStreak_NeverPlayed_IsZero()
    {
        Assert.Equal(0, StreakCalculator.GetEffectiveStreak(UserWith(9, null), Today));
    }

    [Theory]
    [InlineData(null, 0, 1)]
    [InlineData(0, 0, 1)]
    [InlineData(0, 6, 6)]
    [InlineData(-1, 6, 7)]
    [InlineData(-2, 6, 1)]
    public void GetStreakAfterSubmission_FollowsTheCalendarDays(int? lastPlayedDaysAgo, int streak, int expected)
    {
        DateTime? last = lastPlayedDaysAgo is int daysAgo ? Today.Date.AddDays(daysAgo).AddHours(23).AddMinutes(59) : null;

        Assert.Equal(expected, StreakCalculator.GetStreakAfterSubmission(UserWith(streak, last), Today));
    }
}
