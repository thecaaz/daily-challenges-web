using DailyChallenges.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DailyChallenges.IntegrationTests.Infrastructure;

/// <summary>
/// An in-memory SQLite database migrated up to a given migration, so a test can insert legacy rows with raw SQL
/// and then check what the later migrations do to them.
/// </summary>
internal sealed class MigrationTestDb : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public AppDbContext Context { get; }

    private MigrationTestDb()
    {
        _connection.Open();
        Context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
    }

    /// <summary>Creates the database and applies every migration up to and including <paramref name="migration"/>.</summary>
    public static MigrationTestDb MigratedTo(string migration)
    {
        var db = new MigrationTestDb();
        db.Context.GetService<IMigrator>().Migrate(migration);
        return db;
    }

    public void ExecuteSql(string sql) => Context.Database.ExecuteSqlRaw(sql);

    /// <summary>Applies the remaining migrations.</summary>
    public void MigrateToLatest() => Context.Database.Migrate();

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
