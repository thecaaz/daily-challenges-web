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

        public static bool TryParseInt(string s, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;

            // Try direct integer parse first
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv))
            {
                value = iv;
                return true;
            }

            // Fallback: extract numeric token
            var m = ScoreRegex.Match(s);
            if (!m.Success) return false;
            var raw = m.Value.Replace(',', '.');

            // If the token contains a decimal point, only accept if it's a whole number
            if (raw.Contains('.'))
            {
                if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var dv))
                {
                    if (Math.Abs(dv - Math.Round(dv)) < double.Epsilon)
                    {
                        value = (int)Math.Round(dv);
                        return true;
                    }
                    return false;
                }
                return false;
            }

            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }
}
