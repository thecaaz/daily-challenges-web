using System.Globalization;

namespace DailyChallenges.Services
{
    public static class ScoringDayHelper
    {
        /// <summary>
        /// Returns the "scoring day" date for a given UTC timestamp based on the
        /// game's wall-clock reset time and timezone. If the local time of day is
        /// before the reset time, the submission belongs to the previous day.
        /// </summary>
        public static DateTime GetScoringDay(DateTime utcTimestamp, TimeSpan resetTime, string? resetTimezoneId)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(resetTimezoneId ?? "UTC");
                var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcTimestamp, DateTimeKind.Utc), tz);
                var day = local.Date;
                if (local.TimeOfDay < resetTime) day = day.AddDays(-1);
                return day;
            }
            catch (TimeZoneNotFoundException)
            {
                // Fall back to server local time if timezone id is invalid
                var local = utcTimestamp.ToLocalTime();
                var day = local.Date;
                if (local.TimeOfDay < resetTime) day = day.AddDays(-1);
                return day;
            }
        }

        /// <summary>
        /// Returns the current scoring day (i.e. GetScoringDay for DateTime.UtcNow).
        /// </summary>
        public static DateTime GetCurrentScoringDay(TimeSpan resetTime, string? resetTimezoneId)
            => GetScoringDay(DateTime.UtcNow, resetTime, resetTimezoneId);

        /// <summary>
        /// Returns the UTC start/end range for a given scoring-day (local date) based on reset time/timezone.
        /// The range is [UtcStart, UtcEnd).
        /// </summary>
        public static (DateTime UtcStart, DateTime UtcEnd) GetScoringDayUtcRange(DateTime scoringDay, TimeSpan resetTime, string? resetTimezoneId)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(resetTimezoneId ?? "UTC");
                var localStart = scoringDay.Date + resetTime;
                var localEnd = localStart.AddDays(1);
                var utcStart = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localStart, DateTimeKind.Unspecified), tz);
                var utcEnd = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localEnd, DateTimeKind.Unspecified), tz);
                return (utcStart, utcEnd);
            }
            catch (TimeZoneNotFoundException)
            {
                var localStart = scoringDay.Date + resetTime;
                var localEnd = localStart.AddDays(1);
                var utcStart = DateTime.SpecifyKind(localStart.ToUniversalTime(), DateTimeKind.Utc);
                var utcEnd = DateTime.SpecifyKind(localEnd.ToUniversalTime(), DateTimeKind.Utc);
                return (utcStart, utcEnd);
            }
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
