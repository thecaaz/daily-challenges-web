using System.Text.Json;
using DailyChallenges.Json;

namespace DailyChallenges.Tests;

public class UtcDateTimeConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new UtcDateTimeConverter() } };

    private sealed record Holder(DateTime At, DateTime? Maybe);

    [Fact]
    public void Unspecified_values_are_labelled_utc_without_shifting()
    {
        // What SQLite hands back: the stored UTC wall-clock with Kind=Unspecified.
        var value = new DateTime(2026, 4, 1, 10, 30, 0, DateTimeKind.Unspecified);

        Assert.Equal("\"2026-04-01T10:30:00Z\"", JsonSerializer.Serialize(value, Options));
    }

    [Fact]
    public void Utc_values_are_written_with_z()
    {
        var value = new DateTime(2026, 4, 1, 10, 30, 0, 123, DateTimeKind.Utc);

        Assert.Equal("\"2026-04-01T10:30:00.123Z\"", JsonSerializer.Serialize(value, Options));
    }

    [Fact]
    public void Nullable_datetimes_use_the_converter_too()
    {
        var json = JsonSerializer.Serialize(new Holder(new DateTime(2026, 1, 2, 3, 4, 5), new DateTime(2026, 1, 2, 3, 4, 5)), Options);

        Assert.Equal("{\"At\":\"2026-01-02T03:04:05Z\",\"Maybe\":\"2026-01-02T03:04:05Z\"}", json);
        Assert.Equal("{\"At\":\"2026-01-02T03:04:05Z\",\"Maybe\":null}",
            JsonSerializer.Serialize(new Holder(new DateTime(2026, 1, 2, 3, 4, 5), null), Options));
    }

    [Fact]
    public void Reads_offsets_back_as_utc()
    {
        var value = JsonSerializer.Deserialize<DateTime>("\"2026-04-01T12:30:00+02:00\"", Options);

        Assert.Equal(DateTimeKind.Utc, value.Kind);
        Assert.Equal(new DateTime(2026, 4, 1, 10, 30, 0, DateTimeKind.Utc), value);
    }
}
