using System.Globalization;
using System.Text.RegularExpressions;

namespace DailyChallenges.Services
{
    /// <summary>
    /// Parses score strings supplied by users and by the ScoreBridge extension.
    ///
    /// Scores arrive formatted in whatever locale the player's browser used, so
    /// the same value shows up as "40,456" on an en-US machine and "40.456" on a
    /// de-DE one. The separators are indistinguishable by character alone, so we
    /// use the same rule as the frontend's parseScore.js: the last '.' or ',' is
    /// a decimal separator only when fewer than three digits follow it.
    /// Everything else is a grouping separator and gets stripped.
    ///
    /// Consequence: "3.141" parses as 3141, not as pi. Scores with exactly three
    /// decimal places cannot be represented in this format.
    /// </summary>
    public static class ScoreParser
    {
        // A numeric token including any separators *inside* it. Each separator
        // must be followed by more digits, so "3/6" yields "3" and "12 pts"
        // yields "12" - matching the previous first-number-wins behaviour.
        // NBSP (U+00A0) and narrow NBSP (U+202F) are included because fr-FR and
        // ru-RU group with them.
        private static readonly Regex ScoreRegex = new Regex(
            "-?\\d+(?:[.,\\u00A0\\u202F ]\\d+)*", RegexOptions.Compiled);

        private static readonly char[] AllSeparators = { '.', ',', ' ', ' ', ' ' };
        private static readonly char[] DecimalSeparators = { '.', ',' };

        public static double ParseScore(string s)
        {
            return TryParseDouble(s, out var v) ? v : double.NaN;
        }

        public static bool TryParseDouble(string s, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;

            var m = ScoreRegex.Match(s);
            if (!m.Success) return false;
            if (!TryNormalize(m.Value, out var normalized)) return false;

            return double.TryParse(
                normalized,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out value);
        }

        public static bool TryParseInt(string s, out int value)
        {
            value = 0;
            if (TryParseDouble(s, out var doubleVal))
            {
                if (Math.Abs(doubleVal - Math.Round(doubleVal)) < double.Epsilon)
                {
                    value = (int)Math.Round(doubleVal);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Rewrites a locale-formatted numeric token into an invariant-culture
        /// string ("40.456" -> "40456", "1.234,56" -> "1234.56").
        /// </summary>
        private static bool TryNormalize(string token, out string normalized)
        {
            normalized = string.Empty;

            var sign = string.Empty;
            var s = token;
            if (s.StartsWith("-", StringComparison.Ordinal))
            {
                sign = "-";
                s = s.Substring(1);
            }

            var lastSep = s.LastIndexOfAny(DecimalSeparators);
            if (lastSep < 0)
            {
                var plain = StripSeparators(s);
                if (!IsAllDigits(plain)) return false;
                normalized = sign + plain;
                return true;
            }

            var before = s.Substring(0, lastSep);
            var after = s.Substring(lastSep + 1);

            // Fewer than three trailing digits => the last separator is a decimal point.
            if (after.Length > 0 && after.Length < 3)
            {
                before = StripSeparators(before);
                if (!IsAllDigits(before) || !IsAllDigits(after)) return false;
                normalized = sign + before + "." + after;
                return true;
            }

            // Otherwise every separator is a grouping separator.
            var digits = StripSeparators(s);
            if (!IsAllDigits(digits)) return false;
            normalized = sign + digits;
            return true;
        }

        private static string StripSeparators(string s)
        {
            return string.Concat(s.Split(AllSeparators, StringSplitOptions.None));
        }

        private static bool IsAllDigits(string s)
        {
            if (s.Length == 0) return false;
            foreach (var c in s)
            {
                if (c < '0' || c > '9') return false;
            }
            return true;
        }
    }
}
