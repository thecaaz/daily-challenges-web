using Microsoft.Data.Sqlite;

namespace DailyChallenges.Data;

/// <summary>Recognizes startup failures caused by a database file or directory the process may not write.</summary>
public static class DatabaseStartupErrors
{
    private const int SqlitePerm = 3;      // SQLITE_PERM
    private const int SqliteReadOnly = 8;  // SQLITE_READONLY (incl. READONLY_DIRECTORY etc.)
    private const int SqliteCantOpen = 14; // SQLITE_CANTOPEN
    private const string ReadOnlyMessageFragment = "readonly";
    private const string CantOpenMessageFragment = "unable to open";

    /// <summary>
    /// True when <paramref name="ex"/> (or an inner exception) is an access-denied error or an SQLite
    /// "readonly database" / "unable to open database file" error.
    /// </summary>
    public static bool IsNotWritable(Exception ex)
    {
        for (Exception? current = ex; current != null; current = current.InnerException)
        {
            if (IsNotWritableItself(current)) return true;
        }
        return false;
    }

    private static bool IsNotWritableItself(Exception exception) => exception switch
    {
        UnauthorizedAccessException => true,
        SqliteException sqlite => IsSqliteNotWritable(sqlite),
        AggregateException aggregate => aggregate.InnerExceptions.Any(IsNotWritable),
        _ => false,
    };

    private static bool IsSqliteNotWritable(SqliteException sqlite) =>
        sqlite.SqliteErrorCode is SqlitePerm or SqliteReadOnly or SqliteCantOpen
        || sqlite.Message.Contains(ReadOnlyMessageFragment, StringComparison.OrdinalIgnoreCase)
        || sqlite.Message.Contains(CantOpenMessageFragment, StringComparison.OrdinalIgnoreCase);
}
