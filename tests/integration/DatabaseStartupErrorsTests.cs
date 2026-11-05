using DailyChallenges.Data;
using Microsoft.Data.Sqlite;

namespace DailyChallenges.IntegrationTests;

/// <summary>The startup hint for a non-writable database (e.g. a root-owned volume) recognizes real SQLite errors.</summary>
public class DatabaseStartupErrorsTests
{
    [Fact]
    public void Writing_to_a_read_only_database_is_recognized()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dc-readonly-{Guid.NewGuid():N}.db");
        try
        {
            using (var create = new SqliteConnection($"Data Source={path}"))
            {
                create.Open();
                using var cmd = create.CreateCommand();
                cmd.CommandText = "CREATE TABLE t (x INTEGER)";
                cmd.ExecuteNonQuery();
            }
            SqliteConnection.ClearAllPools();

            using var conn = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
            conn.Open();
            using var insert = conn.CreateCommand();
            insert.CommandText = "INSERT INTO t VALUES (1)";
            var ex = Assert.Throws<SqliteException>(() => insert.ExecuteNonQuery());

            Assert.True(DatabaseStartupErrors.IsNotWritable(ex));
            Assert.True(DatabaseStartupErrors.IsNotWritable(new InvalidOperationException("wrapped", ex)));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public void Opening_a_database_in_a_missing_directory_is_recognized()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dc-missing-{Guid.NewGuid():N}", "app.db");
        using var conn = new SqliteConnection($"Data Source={path};Pooling=False");
        var ex = Assert.Throws<SqliteException>(() => conn.Open());

        Assert.True(DatabaseStartupErrors.IsNotWritable(ex));
    }

    [Fact]
    public void Other_errors_are_not_reported_as_permission_problems()
    {
        Assert.True(DatabaseStartupErrors.IsNotWritable(new UnauthorizedAccessException("denied")));
        Assert.False(DatabaseStartupErrors.IsNotWritable(new InvalidOperationException("boom")));
        Assert.False(DatabaseStartupErrors.IsNotWritable(new SqliteException("SQLite Error 1: 'no such table: x'.", 1)));
    }
}
