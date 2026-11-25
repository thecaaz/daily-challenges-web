using System.Globalization;

namespace DailyChallenges.Services;

/// <summary>
/// Maps UTC timestamps to a game's scoring day, given the game's daily reset time-of-day (UTC).
/// <para>
/// A reset before noon UTC <em>starts</em> the scoring day: the day is labelled with the date on which it
/// starts, range [day + reset, day + 1 + reset). A reset at or after noon UTC <em>ends</em> it, because a
/// 22:00 UTC reset feels like the previous calendar day to players: the day is labelled with the date on
/// which it ends, range [day - 1 + reset, day + reset).
/// </para>
/// </summary>
public static class ScoringDayCalculator
{
    public const string DateFormat = "yyyy-MM-dd";

    private static readonly TimeSpan EarliestEndOfDayReset = TimeSpan.FromHours(12);

    /// <summary>The scoring-day label (date only) that <paramref name="utcTimestamp"/> belongs to.</summary>
    public static DateTime GetScoringDay(DateTime utcTimestamp, TimeSpan resetTimeUtc)
    {
        var calendarDay = utcTimestamp.Date;
        var isBeforeReset = utcTimestamp.TimeOfDay < resetTimeUtc;

        if (ResetEndsScoringDay(resetTimeUtc))
            return isBeforeReset ? calendarDay : calendarDay.AddDays(1);

        return isBeforeReset ? calendarDay.AddDays(-1) : calendarDay;
    }

    /// <summary>
    /// The current scoring day for the given "now" (UTC). Services pass
    /// <c>TimeProvider.GetUtcNow().UtcDateTime</c> so tests can control the clock.
    /// </summary>
    public static DateTime GetCurrentScoringDay(TimeSpan resetTimeUtc, DateTime nowUtc)
        => GetScoringDay(nowUtc, resetTimeUtc);

    /// <summary>The UTC range [UtcStart, UtcEnd) of <paramref name="scoringDay"/>.</summary>
    public static (DateTime UtcStart, DateTime UtcEnd) GetScoringDayUtcRange(DateTime scoringDay, TimeSpan resetTimeUtc)
    {
        var resetOnLabelDate = DateTime.SpecifyKind(scoringDay.Date + resetTimeUtc, DateTimeKind.Utc);

        return ResetEndsScoringDay(resetTimeUtc)
            ? (resetOnLabelDate.AddDays(-1), resetOnLabelDate)
            : (resetOnLabelDate, resetOnLabelDate.AddDays(1));
    }

    /// <summary>
    /// Parses a <see cref="DateFormat"/> scoring day. Empty or null input is valid and yields a null day;
    /// returns false only for a malformed date.
    /// </summary>
    public static bool TryParseScoringDay(string? s, out DateTime? scoringDay)
    {
        scoringDay = null;
        if (string.IsNullOrWhiteSpace(s)) return true;
        if (!DateTime.TryParseExact(s, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return false;

        scoringDay = parsed;
        return true;
    }

    public static string FormatScoringDay(DateTime d) => d.ToString(DateFormat);

    private static bool ResetEndsScoringDay(TimeSpan resetTimeUtc) => resetTimeUtc >= EarliestEndOfDayReset;
}
