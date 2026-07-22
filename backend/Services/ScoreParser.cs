using System.Globalization;
using System.Text.RegularExpressions;

namespace DailyChallenges.Services
{
    public static class ScoreParser
    {
        private static readonly Regex ScoreRegex = new Regex("-?\\d+(?:[.,]\\d+)?", RegexOptions.Compiled);

        public static double ParseScore(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return double.NaN;
            var m = ScoreRegex.Match(s);
            if (!m.Success) return double.NaN;
            var raw = m.Value.Replace(',', '.');
            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
            return double.NaN;
        }

        public static bool TryParseDouble(string s, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;

            // Try direct double parse first (handle comma as decimal separator)
            var normalized = s.Replace(',', '.');
            if (double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var dv))
            {
                value = dv;
                return true;
            }

            // Fallback: extract numeric token
            var m = ScoreRegex.Match(s);
            if (!m.Success) return false;
            var raw = m.Value.Replace(',', '.');

            return double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
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
    }
}
