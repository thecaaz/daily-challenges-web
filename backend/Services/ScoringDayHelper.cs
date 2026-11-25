namespace DailyChallenges.Services;

/// <summary>
/// Forwarder kept only while GameService and UserProfileService (refactored on other branches) still call it.
/// Delete it once they use <see cref="ScoringDayCalculator"/>.
/// </summary>
[Obsolete("Use ScoringDayCalculator.")]
public static class ScoringDayHelper
{
    public static DateTime GetScoringDay(DateTime utcTimestamp, TimeSpan resetTimeUtc) =>
        ScoringDayCalculator.GetScoringDay(utcTimestamp, resetTimeUtc);

    public static DateTime GetCurrentScoringDay(TimeSpan resetTimeUtc, DateTime nowUtc) =>
        ScoringDayCalculator.GetCurrentScoringDay(resetTimeUtc, nowUtc);

    public static string FormatScoringDay(DateTime d) => ScoringDayCalculator.FormatScoringDay(d);
}
