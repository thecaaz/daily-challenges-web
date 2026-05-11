using DailyChallenges.Services;

namespace DailyChallenges.Tests;

public class ScoringDayHelperTests
{
    // ResetTimeRepresentsEndOfScoringDay: times >= 12:00 UTC are "end of day"

    // ---- Start-of-day semantics (reset < 12:00) ----

    [Fact]
    public void GetScoringDay_StartOfDay_TimestampAfterReset_ReturnsSameDate()
    {
        var reset = TimeSpan.FromHours(5); // 05:00 UTC
        var ts = new DateTime(2025, 4, 15, 6, 0, 0, DateTimeKind.Utc); // 06:00 UTC
        Assert.Equal(new DateTime(2025, 4, 15), ScoringDayHelper.GetScoringDay(ts, reset));
    }

    [Fact]
    public void GetScoringDay_StartOfDay_TimestampBeforeReset_ReturnsPreviousDate()
    {
        var reset = TimeSpan.FromHours(5); // 05:00 UTC
        var ts = new DateTime(2025, 4, 15, 3, 0, 0, DateTimeKind.Utc); // 03:00 UTC — before reset
        Assert.Equal(new DateTime(2025, 4, 14), ScoringDayHelper.GetScoringDay(ts, reset));
    }

    [Fact]
    public void GetScoringDay_StartOfDay_TimestampExactlyAtReset_ReturnsSameDate()
    {
        var reset = TimeSpan.FromHours(5);
        var ts = new DateTime(2025, 4, 15, 5, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2025, 4, 15), ScoringDayHelper.GetScoringDay(ts, reset));
    }

    // ---- End-of-day semantics (reset >= 12:00) ----

    [Fact]
    public void GetScoringDay_EndOfDay_TimestampBeforeReset_ReturnsSameDate()
    {
        var reset = TimeSpan.FromHours(22); // 22:00 UTC — end-of-day
        var ts = new DateTime(2025, 4, 15, 10, 0, 0, DateTimeKind.Utc); // before reset, label = April 15
        Assert.Equal(new DateTime(2025, 4, 15), ScoringDayHelper.GetScoringDay(ts, reset));
    }

    [Fact]
    public void GetScoringDay_EndOfDay_TimestampAtOrAfterReset_ReturnsNextDate()
    {
        var reset = TimeSpan.FromHours(22); // 22:00 UTC
        var ts = new DateTime(2025, 4, 15, 22, 0, 0, DateTimeKind.Utc); // exactly at reset → next day
        Assert.Equal(new DateTime(2025, 4, 16), ScoringDayHelper.GetScoringDay(ts, reset));
    }

    [Fact]
    public void GetScoringDay_EndOfDay_TimestampAfterReset_ReturnsNextDate()
    {
        var reset = TimeSpan.FromHours(22);
        var ts = new DateTime(2025, 4, 15, 23, 30, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2025, 4, 16), ScoringDayHelper.GetScoringDay(ts, reset));
    }

    // ---- Noon boundary ----

    [Fact]
    public void GetScoringDay_NoonReset_IsEndOfDay()
    {
        // Exactly noon is treated as end-of-day
        var reset = TimeSpan.FromHours(12);
        var ts = new DateTime(2025, 4, 15, 12, 0, 0, DateTimeKind.Utc); // at reset
        Assert.Equal(new DateTime(2025, 4, 16), ScoringDayHelper.GetScoringDay(ts, reset));
    }

    // ---- Midnight reset ----

    [Fact]
    public void GetScoringDay_MidnightReset_StartOfDaySemantics()
    {
        var reset = TimeSpan.Zero; // 00:00 UTC
        var ts = new DateTime(2025, 4, 15, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2025, 4, 15), ScoringDayHelper.GetScoringDay(ts, reset));
    }

    // ---- FormatScoringDay ----

    [Fact]
    public void FormatScoringDay_ReturnsIsoFormat()
    {
        Assert.Equal("2025-04-15", ScoringDayHelper.FormatScoringDay(new DateTime(2025, 4, 15)));
    }

    // ---- TryParseScoringDay ----

    [Fact]
    public void TryParseScoringDay_ValidDate_ReturnsTrueAndDate()
    {
        Assert.True(ScoringDayHelper.TryParseScoringDay("2025-04-15", out var d));
        Assert.Equal(new DateTime(2025, 4, 15), d);
    }

    [Fact]
    public void TryParseScoringDay_NullOrEmpty_ReturnsTrueAndNull()
    {
        Assert.True(ScoringDayHelper.TryParseScoringDay(null, out var d1));
        Assert.Null(d1);
        Assert.True(ScoringDayHelper.TryParseScoringDay("", out var d2));
        Assert.Null(d2);
    }

    [Fact]
    public void TryParseScoringDay_InvalidFormat_ReturnsFalse()
    {
        Assert.False(ScoringDayHelper.TryParseScoringDay("15-04-2025", out _));
        Assert.False(ScoringDayHelper.TryParseScoringDay("not-a-date", out _));
    }

    // ---- GetScoringDayUtcRange ----

    [Fact]
    public void GetScoringDayUtcRange_StartOfDay_RangeStartsAtResetTime()
    {
        var reset = TimeSpan.FromHours(5);
        var day = new DateTime(2025, 4, 15);
        var (start, end) = ScoringDayHelper.GetScoringDayUtcRange(day, reset);
        Assert.Equal(new DateTime(2025, 4, 15, 5, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2025, 4, 16, 5, 0, 0, DateTimeKind.Utc), end);
    }

    [Fact]
    public void GetScoringDayUtcRange_EndOfDay_RangeEndsAtResetTime()
    {
        var reset = TimeSpan.FromHours(22);
        var day = new DateTime(2025, 4, 15);
        var (start, end) = ScoringDayHelper.GetScoringDayUtcRange(day, reset);
        Assert.Equal(new DateTime(2025, 4, 14, 22, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2025, 4, 15, 22, 0, 0, DateTimeKind.Utc), end);
    }
}
