using Microsoft.Data.Sqlite;

namespace DailyChallenges.IntegrationTests.Infrastructure;

/// <summary>Assertions on SQLite error codes (https://www.sqlite.org/rescode.html).</summary>
internal static class SqliteAssert
{
    public const int ConstraintError = 19;
    public const int UniqueConstraintError = 2067;

    /// <summary>Asserts that <paramref name="action"/> fails on a UNIQUE constraint or unique index.</summary>
    public static void UniqueViolation(Action action)
    {
        var ex = Assert.Throws<SqliteException>(action);
        Assert.Equal(ConstraintError, ex.SqliteErrorCode);
        Assert.Equal(UniqueConstraintError, ex.SqliteExtendedErrorCode);
    }
}
