using DailyChallenges.Data;
using DailyChallenges.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;

namespace DailyChallenges.IntegrationTests;

public class UniqueDayWinXpEventMigrationTests
{
    private const string PreviousMigration = "20261104221639_NormalizeImageContentTypes";

    [Fact]
    public void Duplicate_day_wins_are_removed_and_their_xp_subtracted_before_the_index_is_created()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.GetService<IMigrator>().Migrate(PreviousMigration);

        // alice: 50 (submission) + 3×100 for the same win + 100 for another day = 450 XP, level 3.
        // bob: one duplicate (2×100) but an admin already took 150 away: 50 XP left → clamped to 0.
        db.Database.ExecuteSqlRaw(
            "INSERT INTO Users (Id, Username, PasswordHash, IsAdmin, TotalXp, Level, Streak) VALUES " +
            " (1, 'alice', 'x', 0, 450, 3, 0), (2, 'bob', 'x', 0, 50, 1, 0);" +
            "INSERT INTO Games (Id, Name, ResetTime, RankingMode) VALUES (1, 'g', '00:00:00', 0), (2, 'gone', '00:00:00', 0);" +
            "INSERT INTO XpEvents (Id, UserId, GameId, ScoringDay, Amount, EventType, CreatedAt) VALUES " +
            " (1, 1, 1, '2026-07-01 00:00:00', 50, 'submission', '2026-07-01 10:00:00')," +
            " (2, 1, 1, '2026-07-01 00:00:00', 100, 'day_win', '2026-07-02 00:01:00')," +
            " (3, 1, 1, '2026-07-01 00:00:00', 100, 'day_win', '2026-07-02 00:02:00')," +
            " (4, 1, 1, '2026-07-01 00:00:00', 100, 'day_win', '2026-07-02 00:03:00')," +
            " (5, 1, 1, '2026-07-02 00:00:00', 100, 'day_win', '2026-07-03 00:01:00')," +
            " (6, 2, 1, '2026-07-01 00:00:00', 100, 'day_win', '2026-07-02 00:01:00')," +
            " (7, 2, 1, '2026-07-01 00:00:00', 100, 'day_win', '2026-07-02 00:02:00')," +
            " (8, 2, NULL, '2026-07-01 00:00:00', 100, 'day_win', '2026-07-02 00:01:00')," +
            " (9, 2, NULL, '2026-07-01 00:00:00', 100, 'day_win', '2026-07-02 00:02:00');");

        db.Database.Migrate();

        var ids = db.XpEvents.OrderBy(e => e.Id).Select(e => e.Id).ToList();
        Assert.Equal(new[] { 1, 2, 5, 6, 8, 9 }, ids); // lowest Id kept; deleted-game rows (GameId NULL) untouched
        var xp = db.Users.OrderBy(u => u.Id).Select(u => u.TotalXp).ToList();
        Assert.Equal(new long[] { 250, 0 }, xp);

        // Levels are re-derived from TotalXp at startup.
        var fixedUsers = UserLevelSync.Run(db, new LevelCalculator(), NullLogger.Instance);
        Assert.Equal(1, fixedUsers);
        Assert.Equal(new[] { 2, 1 }, db.Users.OrderBy(u => u.Id).Select(u => u.Level).ToList());

        // The index now rejects a second award.
        var ex = Assert.Throws<SqliteException>(() => db.Database.ExecuteSqlRaw(
            "INSERT INTO XpEvents (UserId, GameId, ScoringDay, Amount, EventType, CreatedAt) VALUES (1, 1, '2026-07-01 00:00:00', 100, 'day_win', '2026-07-05 00:00:00');"));
        Assert.Equal(19, ex.SqliteErrorCode);
    }
}
