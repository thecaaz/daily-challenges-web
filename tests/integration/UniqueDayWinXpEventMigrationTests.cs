using DailyChallenges.Data;
using DailyChallenges.IntegrationTests.Infrastructure;
using DailyChallenges.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DailyChallenges.IntegrationTests;

public class UniqueDayWinXpEventMigrationTests
{
    private const string PreviousMigration = "20261104221639_NormalizeImageContentTypes";

    /// <summary>
    /// Seeds duplicate day wins on the previous schema, then applies the migration.
    /// alice: 50 (submission) + 3×100 for the same win + 100 for another day = 450 XP, level 3.
    /// bob: one duplicate (2×100) but an admin already took 150 away: 50 XP left, so the subtraction clamps at 0.
    /// Events 8 and 9 belong to a deleted game (GameId NULL).
    /// </summary>
    private static MigrationTestDb MigrateWithDuplicateDayWins()
    {
        var db = MigrationTestDb.MigratedTo(PreviousMigration);
        db.ExecuteSql(
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
        db.MigrateToLatest();
        return db;
    }

    [Fact]
    public void Duplicate_day_wins_keep_the_lowest_id_and_leave_deleted_game_rows_alone()
    {
        using var db = MigrateWithDuplicateDayWins();

        var ids = db.Context.XpEvents.OrderBy(e => e.Id).Select(e => e.Id).ToList();

        Assert.Equal(new[] { 1, 2, 5, 6, 8, 9 }, ids);
    }

    [Fact]
    public void Duplicate_day_win_xp_is_subtracted_and_clamped_at_zero()
    {
        using var db = MigrateWithDuplicateDayWins();

        var xp = db.Context.Users.OrderBy(u => u.Id).Select(u => u.TotalXp).ToList();

        Assert.Equal(new long[] { 250, 0 }, xp);
    }

    [Fact]
    public void Levels_are_rederived_from_the_corrected_xp_at_startup()
    {
        using var db = MigrateWithDuplicateDayWins();

        var fixedUsers = UserLevelSync.Run(db.Context, new LevelCalculator(), NullLogger.Instance);

        Assert.Equal(1, fixedUsers);
        Assert.Equal(new[] { 2, 1 }, db.Context.Users.OrderBy(u => u.Id).Select(u => u.Level).ToList());
    }

    [Fact]
    public void Unique_index_rejects_a_second_day_win_for_the_same_game_and_day()
    {
        using var db = MigrateWithDuplicateDayWins();

        SqliteAssert.UniqueViolation(() => db.ExecuteSql(
            "INSERT INTO XpEvents (UserId, GameId, ScoringDay, Amount, EventType, CreatedAt) VALUES (1, 1, '2026-07-01 00:00:00', 100, 'day_win', '2026-07-05 00:00:00');"));
    }
}
