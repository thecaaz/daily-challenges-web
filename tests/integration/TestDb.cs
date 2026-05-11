using DailyChallenges.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.IntegrationTests;

/// <summary>
/// Provides an isolated in-memory SQLite AppDbContext for integration tests.
/// Each instance opens its own connection so tests are fully independent.
/// </summary>
internal sealed class TestDb : IAsyncDisposable
{
    private readonly SqliteConnection _conn;

    public AppDbContext Ctx { get; }

    public TestDb()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_conn)
            .Options;
        Ctx = new AppDbContext(opts);
        Ctx.Database.EnsureCreated();
    }

    public async ValueTask DisposeAsync()
    {
        await Ctx.DisposeAsync();
        await _conn.DisposeAsync();
    }
}
