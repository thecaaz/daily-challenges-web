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
            catch
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
    }
}
