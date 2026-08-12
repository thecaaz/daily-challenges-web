using DailyChallenges.Services;

namespace DailyChallenges.Tests;

public class ScoreParserTests
{
    // ---- TryParseDouble ----

    [Theory]
    [InlineData("42", 42.0)]
    [InlineData("-7", -7.0)]
    [InlineData("0", 0.0)]
    [InlineData("1000", 1000.0)]
    public void TryParseDouble_PlainInteger_ReturnsValue(string input, double expected)
    {
        Assert.True(ScoreParser.TryParseDouble(input, out var v));
        Assert.Equal(expected, v);
    }

    [Theory]
    [InlineData("3.14", 3.14)]
    [InlineData("3,14", 3.14)]
    [InlineData("-0.5", -0.5)]
    [InlineData("99.99", 99.99)]
    public void TryParseDouble_AcceptsDecimalValues(string input, double expected)
    {
        Assert.True(ScoreParser.TryParseDouble(input, out var v));
        Assert.Equal(expected, v);
    }

    [Theory]
    [InlineData("3/6", 3.0)]     // Wordle-style: extracts first number
    [InlineData("5/6", 5.0)]
    [InlineData("2:34", 2.0)]    // Time-style: extracts first number
    [InlineData("12 pts", 12.0)]
    [InlineData("  15  ", 15.0)] // Whitespace
    public void TryParseDouble_ScoreWithSuffix_ExtractsFirstNumber(string input, double expected)
    {
        Assert.True(ScoreParser.TryParseDouble(input, out var v));
        Assert.Equal(expected, v);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryParseDouble_NullOrEmpty_ReturnsFalse(string? input)
    {
        Assert.False(ScoreParser.TryParseDouble(input!, out _));
    }

    [Theory]
    [InlineData("no numbers here")]
    [InlineData("abc")]
    public void TryParseDouble_NoNumericToken_ReturnsFalse(string input)
    {
        Assert.False(ScoreParser.TryParseDouble(input, out _));
    }

    // ---- TryParseInt (backward compat) ----

    [Theory]
    [InlineData("42", 42)]
    [InlineData("-7", -7)]
    [InlineData("0", 0)]
    [InlineData("1000", 1000)]
    public void TryParseInt_PlainInteger_ReturnsValue(string input, int expected)
    {
        Assert.True(ScoreParser.TryParseInt(input, out var v));
        Assert.Equal(expected, v);
    }

    [Theory]
    [InlineData("3/6", 3)]     // Wordle-style: extracts first number
    [InlineData("5/6", 5)]
    [InlineData("2:34", 2)]    // Time-style: extracts first number
    [InlineData("12 pts", 12)]
    [InlineData("  15  ", 15)] // Whitespace
    public void TryParseInt_ScoreWithSuffix_ExtractsFirstNumber(string input, int expected)
    {
        Assert.True(ScoreParser.TryParseInt(input, out var v));
        Assert.Equal(expected, v);
    }

    [Theory]
    [InlineData("10.0", 10)]   // Whole-number decimal → accepted
    [InlineData("5,0", 5)]     // Comma decimal
    public void TryParseInt_WholeNumberDecimal_ReturnsValue(string input, int expected)
    {
        Assert.True(ScoreParser.TryParseInt(input, out var v));
        Assert.Equal(expected, v);
    }

    [Theory]
    [InlineData("3.5")]        // True decimal → rejected by TryParseInt
    [InlineData("1.23")]
    public void TryParseInt_NonWholeDecimal_ReturnsFalse(string input)
    {
        Assert.False(ScoreParser.TryParseInt(input, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryParseInt_NullOrEmpty_ReturnsFalse(string? input)
    {
        Assert.False(ScoreParser.TryParseInt(input!, out _));
    }

    [Theory]
    [InlineData("no numbers here")]
    [InlineData("abc")]
    public void TryParseInt_NoNumericToken_ReturnsFalse(string input)
    {
        Assert.False(ScoreParser.TryParseInt(input, out _));
    }

    // ---- ParseScore ----

    [Theory]
    [InlineData("42", 42.0)]
    [InlineData("3.14", 3.14)]
    [InlineData("3/6", 3.0)]
    [InlineData("-5", -5.0)]
    public void ParseScore_ValidInputs_ReturnsDouble(string input, double expected)
    {
        Assert.Equal(expected, ScoreParser.ParseScore(input), precision: 5);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no numbers")]
    public void ParseScore_InvalidInputs_ReturnsNaN(string input)
    {
        Assert.True(double.IsNaN(ScoreParser.ParseScore(input)));
    }

    [Fact]
    public void ParseScore_CommaDecimal_ParsesCorrectly()
    {
        Assert.Equal(3.5, ScoreParser.ParseScore("3,5"), precision: 5);
    }

    // ---- Locale-formatted grouping separators ----
    //
    // Regression: a TimeGuessr score of 40456 renders as "40,456" on en-US and
    // "40.456" on de-DE. Both previously parsed to the decimal 40.456, which
    // sorted the submission below every single-digit score on the leaderboard.

    [Theory]
    [InlineData("40,456", 40456.0)]
    [InlineData("40.456", 40456.0)]
    [InlineData("1,000", 1000.0)]
    [InlineData("1.000", 1000.0)]
    [InlineData("50,000", 50000.0)]
    [InlineData("1.000.000", 1000000.0)]
    [InlineData("1,000,000", 1000000.0)]
    [InlineData("-40.456", -40456.0)]
    public void TryParseDouble_GroupingSeparators_ParsedAsWholeNumber(string input, double expected)
    {
        Assert.True(ScoreParser.TryParseDouble(input, out var v));
        Assert.Equal(expected, v, precision: 5);
    }

    [Theory]
    [InlineData("1.234,56", 1234.56)]   // de-DE
    [InlineData("1,234,567.89", 1234567.89)] // en-US
    [InlineData("1 234 567,89", 1234567.89)] // space grouping
    public void TryParseDouble_MixedSeparators_UsesLastAsDecimal(string input, double expected)
    {
        Assert.True(ScoreParser.TryParseDouble(input, out var v));
        Assert.Equal(expected, v, precision: 5);
    }

    [Fact]
    public void TryParseDouble_NarrowNoBreakSpaceGrouping_ParsedAsWholeNumber()
    {
        // fr-FR groups with U+202F
        Assert.True(ScoreParser.TryParseDouble("40\u202F456", out var v));
        Assert.Equal(40456.0, v, precision: 5);
    }

    [Theory]
    [InlineData("40,456", 40456)]
    [InlineData("40.456", 40456)]
    public void TryParseInt_GroupingSeparators_ParsedAsWholeNumber(string input, int expected)
    {
        Assert.True(ScoreParser.TryParseInt(input, out var v));
        Assert.Equal(expected, v);
    }
}
