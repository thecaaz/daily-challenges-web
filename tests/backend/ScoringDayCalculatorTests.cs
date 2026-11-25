using DailyChallenges.Services;

namespace DailyChallenges.Tests;

public class ScoringDayCalculatorTests
{
    // ResetTimeRepresentsEndOfScoringDay: times >= 12:00 UTC are "end of day"

    // ---- Start-of-day semantics (reset < 12:00) ----

    [Fact]
    public void GetScoringDay_StartOfDay_TimestampAfterReset_ReturnsSameDate()
    {
        var reset = TimeSpan.FromHours(5); // 05:00 UTC
        var ts = new DateTime(2025, 4, 15, 6, 0, 0, DateTimeKind.Utc); // 06:00 UTC
        Assert.Equal(new DateTime(2025, 4, 15), ScoringDayCalculator.GetScoringDay(ts, reset));
    }

    [Fact]
    public void GetScoringDay_StartOfDay_TimestampBeforeReset_ReturnsPreviousDate()
    {
        var reset = TimeSpan.FromHours(5); // 05:00 UTC
        var ts = new DateTime(2025, 4, 15, 3, 0, 0, DateTimeKind.Utc); // 03:00 UTC — before reset
        Assert.Equal(new DateTime(2025, 4, 14), ScoringDayCalculator.GetScoringDay(ts, reset));
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
        var reset = TimeSpan.FromHours(22); // 22:00 UTC — end-of-day
        var ts = new DateTime(2025, 4, 15, 10, 0, 0, DateTimeKind.Utc); // before reset, label = April 15
        Assert.Equal(new DateTime(2025, 4, 15), ScoringDayCalculator.GetScoringDay(ts, reset));
    }

    [Fact]
    public void GetScoringDay_EndOfDay_TimestampAtOrAfterReset_ReturnsNextDate()
    {
        var reset = TimeSpan.FromHours(22); // 22:00 UTC
        var ts = new DateTime(2025, 4, 15, 22, 0, 0, DateTimeKind.Utc); // exactly at reset → next day
        Assert.Equal(new DateTime(2025, 4, 16), ScoringDayCalculator.GetScoringDay(ts, reset));
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
    public void GetScoringDay_NoonReset_IsEndOfDay()
    {
        // Exactly noon is treated as end-of-day
        var reset = TimeSpan.FromHours(12);
        var ts = new DateTime(2025, 4, 15, 12, 0, 0, DateTimeKind.Utc); // at reset
        Assert.Equal(new DateTime(2025, 4, 16), ScoringDayCalculator.GetScoringDay(ts, reset));
    }

    // ---- Midnight reset ----

    [Fact]
    public void GetScoringDay_MidnightReset_StartOfDaySemantics()
    {
        var reset = TimeSpan.Zero; // 00:00 UTC
        var ts = new DateTime(2025, 4, 15, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2025, 4, 15), ScoringDayCalculator.GetScoringDay(ts, reset));
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
