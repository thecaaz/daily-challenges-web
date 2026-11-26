namespace DailyChallenges.Services;

/// <summary>
/// The ended scoring days [<see cref="FirstDay"/>, <see cref="LastDay"/>] (dates, inclusive) the background
/// service checks for un-finalized days on a tick.
/// </summary>
public sealed record ScoringDayCatchUpWindow(DateTime FirstDay, DateTime LastDay)
{
    /// <summary>
    /// The window is [max(firstSubmissionDay, min(lastFinalizedDay + 1, currentDay - gapScanDays)), currentDay - 1]:
    /// every gap in the last <paramref name="gapScanDays"/> days is retried (e.g. a day that was un-finalized
    /// by an admin edit/delete whose re-finalization failed), and after an even longer outage the catch-up
    /// still starts right after the last finalized day. The current day is still in progress, so the window
    /// ends the day before it. Returns null when there is no ended day to check.
    /// </summary>
    public static ScoringDayCatchUpWindow? For(
        DateTime currentDay, DateTime? lastFinalizedDay, DateTime firstSubmissionDay, int gapScanDays)
    {
        var lastEndedDay = currentDay.Date.AddDays(-1);
        var afterLastFinalized = lastFinalizedDay?.Date.AddDays(1) ?? firstSubmissionDay.Date;
        var scanStart = currentDay.Date.AddDays(-gapScanDays);
        var firstDay = Max(firstSubmissionDay.Date, Min(afterLastFinalized, scanStart));

        return firstDay > lastEndedDay ? null : new ScoringDayCatchUpWindow(firstDay, lastEndedDay);
    }

    /// <summary>The window's days that are not in <paramref name="finalizedDays"/>, oldest first.</summary>
    public IEnumerable<DateTime> UnfinalizedDays(IReadOnlySet<DateTime> finalizedDays)
    {
        for (var day = FirstDay; day <= LastDay; day = day.AddDays(1))
        {
            if (!finalizedDays.Contains(day))
                yield return day;
        }
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
}
