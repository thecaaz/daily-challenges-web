using DailyChallenges.Services;

namespace DailyChallenges.Tests;

/// <summary>A reset time of 12:00 UTC or later marks the end of a scoring day; an earlier one marks its start.</summary>
public class ScoringDayCalculatorTests
{
    // ---- Start-of-day semantics (reset < 12:00) ----

    [Fact]
    public void GetScoringDay_StartOfDay_TimestampAfterReset_ReturnsSameDate()
    {
        var reset = TimeSpan.FromHours(5);
        var afterReset = new DateTime(2025, 4, 15, 6, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2025, 4, 15), ScoringDayCalculator.GetScoringDay(afterReset, reset));
    }

    [Fact]
    public void GetScoringDay_StartOfDay_TimestampBeforeReset_ReturnsPreviousDate()
    {
        var reset = TimeSpan.FromHours(5);
        var beforeReset = new DateTime(2025, 4, 15, 3, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2025, 4, 14), ScoringDayCalculator.GetScoringDay(beforeReset, reset));
    }

    [Fact]
    public void GetScoringDay_StartOfDay_TimestampExactlyAtReset_ReturnsSameDate()
    {
        var reset = TimeSpan.FromHours(5);
        var ts = new DateTime(2025, 4, 15, 5, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2025, 4, 15), ScoringDayCalculator.GetScoringDay(ts, reset));
    }

    // ---- End-of-day semantics (reset >= 12:00) ----

    [Fact]
    public void GetScoringDay_EndOfDay_TimestampBeforeReset_ReturnsSameDate()
    {
        var endOfDayReset = TimeSpan.FromHours(22);
        var beforeReset = new DateTime(2025, 4, 15, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2025, 4, 15), ScoringDayCalculator.GetScoringDay(beforeReset, endOfDayReset));
    }

    [Fact]
    public void GetScoringDay_EndOfDay_TimestampExactlyAtReset_ReturnsNextDate()
    {
        var reset = TimeSpan.FromHours(22);
        var atReset = new DateTime(2025, 4, 15, 22, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2025, 4, 16), ScoringDayCalculator.GetScoringDay(atReset, reset));
    }

    [Fact]
    public void GetScoringDay_EndOfDay_TimestampAfterReset_ReturnsNextDate()
    {
        var reset = TimeSpan.FromHours(22);
        var ts = new DateTime(2025, 4, 15, 23, 30, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2025, 4, 16), ScoringDayCalculator.GetScoringDay(ts, reset));
    }

    // ---- Noon boundary ----

    [Fact]
    public void GetScoringDay_NoonResetExactlyAtNoon_BelongsToTheNextDay()
    {
        var reset = TimeSpan.FromHours(12);
        var atReset = new DateTime(2025, 4, 15, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2025, 4, 16), ScoringDayCalculator.GetScoringDay(atReset, reset));
    }

    // ---- Midnight reset ----

    [Fact]
    public void GetScoringDay_MidnightReset_StartOfDaySemantics()
    {
        var midnight = TimeSpan.Zero;
        var ts = new DateTime(2025, 4, 15, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2025, 4, 15), ScoringDayCalculator.GetScoringDay(ts, midnight));
    }

    // ---- FormatScoringDay ----

    [Fact]
    public void FormatScoringDay_ReturnsIsoFormat()
    {
        Assert.Equal("2025-04-15", ScoringDayCalculator.FormatScoringDay(new DateTime(2025, 4, 15)));
    }

    // ---- TryParseScoringDay ----

    [Fact]
    public void TryParseScoringDay_ValidDate_ReturnsTrueAndDate()
    {
        Assert.True(ScoringDayCalculator.TryParseScoringDay("2025-04-15", out var d));
        Assert.Equal(new DateTime(2025, 4, 15), d);
    }

    [Fact]
    public void TryParseScoringDay_NullOrEmpty_ReturnsTrueAndNull()
    {
        Assert.True(ScoringDayCalculator.TryParseScoringDay(null, out var d1));
        Assert.Null(d1);
        Assert.True(ScoringDayCalculator.TryParseScoringDay("", out var d2));
        Assert.Null(d2);
    }

    [Fact]
    public void TryParseScoringDay_InvalidFormat_ReturnsFalse()
    {
        Assert.False(ScoringDayCalculator.TryParseScoringDay("15-04-2025", out _));
        Assert.False(ScoringDayCalculator.TryParseScoringDay("not-a-date", out _));
    }

    // ---- GetScoringDayUtcRange ----

    [Fact]
    public void GetScoringDayUtcRange_StartOfDay_RangeStartsAtResetTime()
    {
        var reset = TimeSpan.FromHours(5);
        var day = new DateTime(2025, 4, 15);
        var (start, end) = ScoringDayCalculator.GetScoringDayUtcRange(day, reset);
        Assert.Equal(new DateTime(2025, 4, 15, 5, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2025, 4, 16, 5, 0, 0, DateTimeKind.Utc), end);
    }

    [Fact]
    public void GetScoringDayUtcRange_EndOfDay_RangeEndsAtResetTime()
    {
        var reset = TimeSpan.FromHours(22);
        var day = new DateTime(2025, 4, 15);
        var (start, end) = ScoringDayCalculator.GetScoringDayUtcRange(day, reset);
        Assert.Equal(new DateTime(2025, 4, 14, 22, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2025, 4, 15, 22, 0, 0, DateTimeKind.Utc), end);
    }

    // ---- Current scoring day with an explicit clock ----

    [Theory]
    [InlineData(4, 59, 14)] // before a 05:00 reset -> previous day
    [InlineData(5, 0, 15)]  // at the reset -> new day
    public void GetCurrentScoringDay_UsesSuppliedNow(int hour, int minute, int expectedDay)
    {
        var now = new DateTime(2025, 4, 15, hour, minute, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2025, 4, expectedDay), ScoringDayCalculator.GetCurrentScoringDay(TimeSpan.FromHours(5), now));
    }
}
