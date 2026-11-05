using System.Globalization;
using System.Text.RegularExpressions;

namespace DailyChallenges.Services
{
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
    /// Invalid input yields false / NaN; callers store a null ScoreValue (unranked).
    /// </summary>
    public static class ScoreParser
    {
        // [0-9] rather than \d: .NET's \d also matches non-ASCII digits.
        private const string Sep = "[.,\\u0020\\u00A0\\u202F'\\u2019]";

        private static readonly Regex Grouped = new($"^[0-9]{{1,3}}(?:{Sep}[0-9]{{3}})+$", RegexOptions.Compiled);
        private static readonly Regex Separators = new(Sep, RegexOptions.Compiled);
        private static readonly Regex NumberChars = new($"^(?:[0-9]|{Sep})*$", RegexOptions.Compiled);
        private static readonly Regex Digits = new("^[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex Time = new(
            "^([0-9]+):([0-5][0-9])(?::([0-5][0-9]))?(?:[.,]([0-9]{1,3}))?$", RegexOptions.Compiled);

        /// <summary>The parsed value, or NaN when <paramref name="s"/> is not a valid score.</summary>
        public static double ParseScore(string? s)
        {
            return TryParseDouble(s, out var v) ? v : double.NaN;
        }

        public static bool TryParseDouble(string? s, out double value)
        {
            value = 0;
            if (s == null) return false;
            var str = s.Trim();
            if (str.Length == 0) return false;

            if (str.Contains(':')) return TryParseTime(str, out value);

            var sign = string.Empty;
            if (str[0] == '+' || str[0] == '-')
            {
                sign = str[0] == '-' ? "-" : string.Empty;
                str = str.Substring(1);
            }
            if (str.Length == 0 || !NumberChars.IsMatch(str)) return false;

            var intPart = str;
            var frac = string.Empty;
            var idx = Math.Max(str.LastIndexOf('.'), str.LastIndexOf(','));
            if (idx >= 0)
            {
                var before = str.Substring(0, idx);
                var after = str.Substring(idx + 1);
                if (Digits.IsMatch(after) && (after.Length <= 2 || before == "0" || before.Length == 0))
                {
                    intPart = before.Length == 0 ? "0" : before;
                    frac = after;
                }
            }

            string digits;
            if (Digits.IsMatch(intPart)) digits = intPart;
            else if (Grouped.IsMatch(intPart)) digits = Separators.Replace(intPart, string.Empty);
            else return false;

            return TryFinite(sign + digits + (frac.Length > 0 ? "." + frac : string.Empty), out value);
        }

        private static bool TryParseTime(string s, out double value)
        {
            value = 0;
            var m = Time.Match(s);
            if (!m.Success) return false;

            // A very long first field overflows to infinity and is rejected by TryFinite.
            if (!double.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var first)) return false;
            var second = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            double total = m.Groups[3].Success
                ? first * 3600 + second * 60 + int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture)
                : first * 60 + second;

            var frac = m.Groups[4].Success ? "." + m.Groups[4].Value : string.Empty;
            return TryFinite(total.ToString("R", CultureInfo.InvariantCulture) + frac, out value);
        }

        private static bool TryFinite(string invariant, out double value)
        {
            if (!double.TryParse(invariant, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                    CultureInfo.InvariantCulture, out value) || !double.IsFinite(value))
            {
                value = 0;
                return false;
            }
            if (value == 0) value = 0; // no -0
            return true;
        }
    }
}
