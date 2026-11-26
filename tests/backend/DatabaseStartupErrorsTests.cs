using DailyChallenges.Data;
using Microsoft.Data.Sqlite;

namespace DailyChallenges.Tests;

public class DatabaseStartupErrorsTests
{
    private const int SqliteError = 1;
    private const int SqlitePerm = 3;
    private const int SqliteReadOnly = 8;
    private const int SqliteCantOpen = 14;

    private static SqliteException NonMatchingSqliteError() => new("SQLite Error 1: 'no such table: x'.", SqliteError);

    [Fact]
    public void IsNotWritable_UnauthorizedAccess_ReturnsTrue()
    {
        Assert.True(DatabaseStartupErrors.IsNotWritable(new UnauthorizedAccessException("denied")));
    }

    [Theory]
    [InlineData(SqlitePerm)]
    [InlineData(SqliteReadOnly)]
    [InlineData(SqliteCantOpen)]
    public void IsNotWritable_SqliteErrorCodeForAccessProblem_ReturnsTrue(int errorCode)
    {
        Assert.True(DatabaseStartupErrors.IsNotWritable(new SqliteException("SQLite Error", errorCode)));
    }

    [Theory]
    [InlineData("attempt to write a READONLY database")]
    [InlineData("Unable to open database file")]
    public void IsNotWritable_SqliteMessageForAccessProblem_ReturnsTrue(string message)
    {
        Assert.True(DatabaseStartupErrors.IsNotWritable(new SqliteException(message, SqliteError)));
    }

    [Fact]
    public void IsNotWritable_AccessProblemAsInnerException_ReturnsTrue()
    {
        var wrapped = new InvalidOperationException("outer", new InvalidOperationException("middle", new UnauthorizedAccessException("denied")));

        Assert.True(DatabaseStartupErrors.IsNotWritable(wrapped));
    }

    [Fact]
    public void IsNotWritable_AccessProblemInsideAggregate_ReturnsTrue()
    {
        var aggregate = new AggregateException(new InvalidOperationException("boom"), new SqliteException("SQLite Error", SqliteReadOnly));

        Assert.True(DatabaseStartupErrors.IsNotWritable(new InvalidOperationException("wrapped", aggregate)));
    }

    [Fact]
    public void IsNotWritable_AggregateWithoutAccessProblem_ReturnsFalse()
    {
        var aggregate = new AggregateException(new InvalidOperationException("boom"), NonMatchingSqliteError());

        Assert.False(DatabaseStartupErrors.IsNotWritable(aggregate));
    }

    [Fact]
    public void IsNotWritable_UnrelatedException_ReturnsFalse()
    {
        Assert.False(DatabaseStartupErrors.IsNotWritable(new InvalidOperationException("boom", new ArgumentException("bad"))));
    }

    [Fact]
    public void IsNotWritable_UnrelatedSqliteError_ReturnsFalse()
    {
        Assert.False(DatabaseStartupErrors.IsNotWritable(NonMatchingSqliteError()));
    }
}
