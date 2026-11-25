using DailyChallenges.Services;

namespace DailyChallenges.Tests;

public class ScoringDayCatchUpWindowTests
{
    private const int GapScanDays = 90;
    private static readonly DateTime CurrentDay = new(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void For_NothingFinalized_StartsAtTheFirstSubmissionDay()
    {
        var window = ScoringDayCatchUpWindow.For(CurrentDay, null, CurrentDay.AddDays(-3), GapScanDays);

        Assert.Equal(new ScoringDayCatchUpWindow(CurrentDay.AddDays(-3), CurrentDay.AddDays(-1)), window);
    }

    [Fact]
    public void For_RecentlyFinalized_StillRescansTheGapWindow()
    {
        var window = ScoringDayCatchUpWindow.For(CurrentDay, CurrentDay.AddDays(-1), CurrentDay.AddDays(-400), GapScanDays);

        Assert.Equal(CurrentDay.AddDays(-GapScanDays), window!.FirstDay);
    }

    [Fact]
    public void For_LongOutage_StartsRightAfterTheLastFinalizedDay()
    {
        var window = ScoringDayCatchUpWindow.For(CurrentDay, CurrentDay.AddDays(-200), CurrentDay.AddDays(-400), GapScanDays);

        Assert.Equal(CurrentDay.AddDays(-199), window!.FirstDay);
    }

    [Fact]
    public void For_OnlyTodaysSubmissions_HasNoEndedDay()
    {
        Assert.Null(ScoringDayCatchUpWindow.For(CurrentDay, null, CurrentDay, GapScanDays));
    }

    [Fact]
    public void UnfinalizedDays_SkipsFinalizedDaysOldestFirst()
    {
        var window = new ScoringDayCatchUpWindow(CurrentDay.AddDays(-4), CurrentDay.AddDays(-1));
        var finalized = new HashSet<DateTime> { CurrentDay.AddDays(-3), CurrentDay.AddDays(-1) };

        Assert.Equal(new[] { CurrentDay.AddDays(-4), CurrentDay.AddDays(-2) }, window.UnfinalizedDays(finalized));
    }
}
