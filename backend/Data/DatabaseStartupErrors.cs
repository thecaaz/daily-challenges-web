using Microsoft.Data.Sqlite;

namespace DailyChallenges.Data
{
    /// <summary>Recognizes startup failures caused by a database file or directory the process may not write.</summary>
    public static class DatabaseStartupErrors
    {
        private const int SqlitePerm = 3;      // SQLITE_PERM
        private const int SqliteReadOnly = 8;  // SQLITE_READONLY (incl. READONLY_DIRECTORY etc.)
        private const int SqliteCantOpen = 14; // SQLITE_CANTOPEN

        /// <summary>
        /// True when <paramref name="ex"/> (or an inner exception) is an access-denied error or an SQLite
        /// "readonly database" / "unable to open database file" error.
        /// </summary>
        public static bool IsNotWritable(Exception ex)
        {
            for (Exception? e = ex; e != null; e = e.InnerException)
            {
                if (e is UnauthorizedAccessException) return true;
                if (e is SqliteException sqlite)
                {
                    if (sqlite.SqliteErrorCode is SqlitePerm or SqliteReadOnly or SqliteCantOpen) return true;
                    var message = sqlite.Message;
                    if (message.Contains("readonly", StringComparison.OrdinalIgnoreCase)
                        || message.Contains("unable to open", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                if (e is AggregateException agg && agg.InnerExceptions.Any(IsNotWritable)) return true;
            }
            return false;
        }
    }
}
