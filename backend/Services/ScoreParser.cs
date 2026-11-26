using System.Globalization;
using System.Text.RegularExpressions;

namespace DailyChallenges.Services;

/// <summary>
/// Parses score strings supplied by users and by the ScoreBridge extension.
///
/// Follows the shared spec in tests/fixtures/score-parsing.json, which the frontend's
/// parseScore.js and extension/scoreparse.js implement too:
/// - The WHOLE trimmed string must be a score; surrounding text makes it invalid.
/// - Optional leading '+'/'-' (numbers only, not times).
/// - Times 'm:ss' and 'h:mm:ss' (optionally '.f'/',f', 1-3 digits) are converted to total seconds.
/// - Grouping separators: ',', '.', space, NBSP, narrow NBSP, apostrophe and right single quote.
///   With grouping, the first group has 1-3 digits and every following group exactly 3.
/// - The last '.'/',' is the decimal point when followed by 1-2 digits, or when the integer part
///   before it is '0' or empty. Otherwise it is a grouping separator, so "1.234" is 1234.
/// Invalid input yields false; callers store a null ScoreValue (unranked).
/// </summary>
public static class ScoreParser
{
    private const char TimeSeparator = ':';
    private const int SecondsPerMinute = 60;
    private const int SecondsPerHour = 3600;
    private const int MaxDecimalDigits = 2;

    /// <summary>Grouping/decimal separators. Patterns use [0-9] rather than \d: .NET's \d also matches non-ASCII digits.</summary>
    private const string SeparatorClass = "[.,\\u0020\\u00A0\\u202F'\\u2019]";

    private static readonly Regex GroupedDigits = new($"^[0-9]{{1,3}}(?:{SeparatorClass}[0-9]{{3}})+$", RegexOptions.Compiled);
    private static readonly Regex Separators = new(SeparatorClass, RegexOptions.Compiled);
    private static readonly Regex NumberChars = new($"^(?:[0-9]|{SeparatorClass})*$", RegexOptions.Compiled);
    private static readonly Regex Digits = new("^[0-9]+$", RegexOptions.Compiled);

    /// <summary>m:ss or h:mm:ss, optional 1-3 digit fraction after '.' or ','.</summary>
    private static readonly Regex Time = new(
        "^([0-9]+):([0-5][0-9])(?::([0-5][0-9]))?(?:[.,]([0-9]{1,3}))?$", RegexOptions.Compiled);

    public static bool TryParseDouble(string? score, out double value)
    {
        value = 0;
        var text = score?.Trim();
        if (string.IsNullOrEmpty(text)) return false;

        if (text.Contains(TimeSeparator)) return TryParseTime(text, out value);

        var (sign, unsigned) = SplitSign(text);
        if (unsigned.Length == 0 || !NumberChars.IsMatch(unsigned)) return false;

        var (integer, fraction) = SplitDecimalPoint(unsigned);
        var digits = IntegerDigits(integer);
        return digits != null && TryToFiniteNumber(sign, digits, fraction, out value);
    }

    /// <summary>
    /// Total seconds of an m:ss or h:mm:ss time, keeping its fraction. A very long first field
    /// overflows to infinity and is rejected by <see cref="TryToFiniteNumber"/>.
    /// </summary>
    private static bool TryParseTime(string text, out double value)
    {
        value = 0;
        var match = Time.Match(text);
        if (!match.Success) return false;

        if (!double.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var first)) return false;
        var second = ParseInvariantInt(match.Groups[2].Value);
        var hasHours = match.Groups[3].Success;
        double totalSeconds = hasHours
            ? first * SecondsPerHour + second * SecondsPerMinute + ParseInvariantInt(match.Groups[3].Value)
            : first * SecondsPerMinute + second;

        var fraction = match.Groups[4].Success ? match.Groups[4].Value : string.Empty;
        return TryToFiniteNumber(string.Empty, totalSeconds.ToString("R", CultureInfo.InvariantCulture), fraction, out value);
    }

    private static int ParseInvariantInt(string digits) => int.Parse(digits, CultureInfo.InvariantCulture);

    private static (string Sign, string Unsigned) SplitSign(string text) => text[0] switch
    {
        '-' => ("-", text[1..]),
        '+' => (string.Empty, text[1..]),
        _ => (string.Empty, text),
    };

    /// <summary>
    /// The last '.' or ',' is a decimal point when at most <see cref="MaxDecimalDigits"/> digits follow it
    /// or nothing but '0' precedes it ("0.125", ".5"); otherwise it is a grouping separator ("40.456").
    /// </summary>
    private static (string Integer, string Fraction) SplitDecimalPoint(string text)
    {
        var pointIndex = Math.Max(text.LastIndexOf('.'), text.LastIndexOf(','));
        if (pointIndex < 0) return WholeNumber(text);

        var before = text[..pointIndex];
        var after = text[(pointIndex + 1)..];
        var hasNoIntegerPart = before == "0" || before.Length == 0;
        var isDecimalPoint = Digits.IsMatch(after) && (after.Length <= MaxDecimalDigits || hasNoIntegerPart);
        if (!isDecimalPoint) return WholeNumber(text);

        return (before.Length == 0 ? "0" : before, after);
    }

    private static (string Integer, string Fraction) WholeNumber(string text) => (text, string.Empty);

    /// <summary>Plain digits, or digits in groups of three without the separators; null otherwise.</summary>
    private static string? IntegerDigits(string integer)
    {
        if (Digits.IsMatch(integer)) return integer;
        if (GroupedDigits.IsMatch(integer)) return Separators.Replace(integer, string.Empty);
        return null;
    }

    private static bool TryToFiniteNumber(string sign, string digits, string fraction, out double value)
    {
        var invariant = sign + digits + (fraction.Length > 0 ? "." + fraction : string.Empty);
        if (!double.TryParse(invariant, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture, out value) || !double.IsFinite(value))
        {
            value = 0;
            return false;
        }
        value = WithoutNegativeZero(value);
        return true;
    }

    /// <summary>-0 and 0 compare equal; returning the literal 0 keeps "-0" from being stored or displayed.</summary>
    private static double WithoutNegativeZero(double value) => value == 0 ? 0 : value;
}
