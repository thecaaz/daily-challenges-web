using DailyChallenges.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.IntegrationTests;

public class ModelSnapshotTests
{
    /// <summary>
    /// EF Core 9+ makes Migrate() throw (PendingModelChangesWarning) when the model differs from the
    /// latest migration's snapshot, which would stop the app at startup. A model change needs a migration.
    /// </summary>
    [Fact]
    public void Model_matches_the_latest_migration_snapshot()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);

        Assert.False(db.Database.HasPendingModelChanges(),
            "The model has changes not captured in a migration: run `dotnet ef migrations add <Name>`.");
    }
}
