using System.Globalization;

namespace DailyChallenges.Services
{
    public static class ScoringDayHelper
    {
        /// <summary>
        /// Returns the "scoring day" date for a given UTC timestamp based on the
        /// game's reset time-of-day expressed in UTC. If the UTC time-of-day is
        /// before the reset time, the submission belongs to the previous day.
        /// </summary>
        public static DateTime GetScoringDay(DateTime utcTimestamp, TimeSpan resetTimeUtc)
        {
            var day = utcTimestamp.Date;
            if (utcTimestamp.TimeOfDay < resetTimeUtc) day = day.AddDays(-1);
            return day;
        }

        /// <summary>
        /// Returns the current scoring day (i.e. GetScoringDay for DateTime.UtcNow).
        /// </summary>
        public static DateTime GetCurrentScoringDay(TimeSpan resetTimeUtc)
            => GetScoringDay(DateTime.UtcNow, resetTimeUtc);

        /// <summary>
        /// Returns the UTC start/end range for a given scoring-day based on a UTC reset time-of-day.
        /// The range is [UtcStart, UtcEnd).
        /// </summary>
        public static (DateTime UtcStart, DateTime UtcEnd) GetScoringDayUtcRange(DateTime scoringDay, TimeSpan resetTimeUtc)
        {
            var utcStart = DateTime.SpecifyKind(scoringDay.Date + resetTimeUtc, DateTimeKind.Utc);
            var utcEnd = utcStart.AddDays(1);
            return (utcStart, utcEnd);
        }

        /// <summary>
        /// Try to parse a scoring-day string in yyyy-MM-dd format. Returns true for empty/null input
        /// and sets scoringDay to null in that case. Returns false on invalid format.
        /// </summary>
        public static bool TryParseScoringDay(string? s, out DateTime? scoringDay)
        {
            scoringDay = null;
            if (string.IsNullOrWhiteSpace(s)) return true;
            if (DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                scoringDay = dt;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Format a scoring-day DateTime as yyyy-MM-dd.
        /// </summary>
        public static string FormatScoringDay(DateTime d) => d.ToString("yyyy-MM-dd");
    }
}
