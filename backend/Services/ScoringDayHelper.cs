using System.Globalization;

namespace DailyChallenges.Services
{
    public static class ScoringDayHelper
    {
        /// <summary>
        /// Returns the "scoring day" date for a given UTC timestamp based on the
        /// game's reset time-of-day expressed in UTC. If the UTC time-of-day is
        /// before the reset time, the submission belongs to the previous day.
        /// 
        /// IMPORTANT: Historically the code treated `resetTimeUtc` as the start
        /// of the scoring day (scoring day label = start date). To better match
        /// UX expectations for late reset times (e.g. 22:00 UTC), we treat
        /// reset times at or after noon (12:00 UTC) as the *end* of the
        /// scoring day (scoring day label = end date). For those cases the
        /// scoring-day range is [scoringDay.Date - 1 + resetTimeUtc, scoringDay.Date + resetTimeUtc).
        /// </summary>
        public static DateTime GetScoringDay(DateTime utcTimestamp, TimeSpan resetTimeUtc)
        {
            var day = utcTimestamp.Date;
            // If reset time is considered the end of the scoring day (late times),
            // then timestamps before the reset belong to the same-day label,
            // otherwise they belong to the previous day (start-of-day semantics).
            if (ResetTimeRepresentsEndOfScoringDay(resetTimeUtc))
            {
                // End-of-day semantics: if timestamp is at-or-after reset, it's for the next day's label.
                if (utcTimestamp.TimeOfDay >= resetTimeUtc) day = day.AddDays(1);
            }
            else
            {
                // Start-of-day semantics (original behavior).
                if (utcTimestamp.TimeOfDay < resetTimeUtc) day = day.AddDays(-1);
            }
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
            if (ResetTimeRepresentsEndOfScoringDay(resetTimeUtc))
            {
                // resetTime is the end of the scoring day: range = [scoringDay.Date - 1 + resetTime, scoringDay.Date + resetTime)
                var utcEnd = DateTime.SpecifyKind(scoringDay.Date + resetTimeUtc, DateTimeKind.Utc);
                var utcStart = utcEnd.AddDays(-1);
                return (utcStart, utcEnd);
            }
            else
            {
                // resetTime is the start of the scoring day: range = [scoringDay.Date + resetTime, scoringDay.Date + resetTime + 1day)
                var utcStart = DateTime.SpecifyKind(scoringDay.Date + resetTimeUtc, DateTimeKind.Utc);
                var utcEnd = utcStart.AddDays(1);
                return (utcStart, utcEnd);
            }
        }

        private static bool ResetTimeRepresentsEndOfScoringDay(TimeSpan resetTimeUtc)
        {
            // Heuristic: treat reset times at or after 12:00 UTC as the end of the scoring day.
            // This matches the described UX where a 22:00 UTC reset feels like the previous calendar day.
            return resetTimeUtc >= TimeSpan.FromHours(12);
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
