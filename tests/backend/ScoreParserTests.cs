using System.Text.Json;
using DailyChallenges.Services;

namespace DailyChallenges.Tests;

/// <summary>
/// ScoreParser against the shared spec in tests/fixtures/score-parsing.json (copied to the test
/// output by the csproj). The frontend and extension parsers run the same vectors in vitest.
/// </summary>
public class ScoreParserTests
{
    private static readonly string FixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "score-parsing.json");

    public static IEnumerable<object?[]> FixtureCases()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(FixturePath));
        foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            var input = c.GetProperty("input").GetString()!;
            var expected = c.GetProperty("expected");
            yield return new object?[] { input, expected.ValueKind == JsonValueKind.Null ? null : expected.GetDouble() };
        }
    }

    [Fact]
    public void Fixture_has_cases()
    {
        Assert.True(FixtureCases().Count() >= 40);
    }

    [Theory]
    [MemberData(nameof(FixtureCases))]
    public void Matches_shared_spec(string input, double? expected)
    {
        var ok = ScoreParser.TryParseDouble(input, out var value);
        if (expected == null)
        {
            Assert.False(ok, $"'{input}' should be invalid but parsed as {value}");
            Assert.True(double.IsNaN(ScoreParser.ParseScore(input)));
        }
        else
        {
            Assert.True(ok, $"'{input}' should parse as {expected}");
            Assert.Equal(expected.Value, value, precision: 9);
            Assert.Equal(expected.Value, ScoreParser.ParseScore(input), precision: 9);
        }
    }

    // ── Extra targeted cases ─────────────────────────────────────────────────

    [Fact]
    public void Null_is_invalid()
    {
        Assert.False(ScoreParser.TryParseDouble(null, out _));
        Assert.True(double.IsNaN(ScoreParser.ParseScore(null)));
    }

    [Theory]
    [InlineData("3/6")]          // the old first-number logic gave 3
    [InlineData("12 pts")]       // ... and 12
    [InlineData("1e+21")]        // String(Number) of a huge value
    [InlineData("Infinity")]
    [InlineData("NaN")]
    public void Old_first_token_inputs_are_now_invalid(string input)
    {
        Assert.False(ScoreParser.TryParseDouble(input, out _));
    }

    [Theory]
    [InlineData("١٢٣")] // Arabic-Indic digits: only ASCII digits count
    [InlineData("１２")]               // full-width digits
    public void Non_ascii_digits_are_invalid(string input)
    {
        Assert.False(ScoreParser.TryParseDouble(input, out _));
    }

    [Fact]
    public void Narrow_nbsp_grouping_and_tabs_trimmed()
    {
        Assert.True(ScoreParser.TryParseDouble("\t40 456\n", out var v));
        Assert.Equal(40456, v);
    }

    [Fact]
    public void Negative_zero_is_zero()
    {
        Assert.True(ScoreParser.TryParseDouble("-0", out var v));
        Assert.Equal(0.0, v);
        Assert.False(double.IsNegative(v));
    }

    [Fact]
    public void Absurdly_long_digit_string_is_invalid()
    {
        Assert.False(ScoreParser.TryParseDouble(new string('9', 400), out _));
    }

    [Fact]
    public void Time_with_three_fraction_digits()
    {
        Assert.True(ScoreParser.TryParseDouble("1:02.345", out var v));
        Assert.Equal(62.345, v, precision: 9);
    }
}
