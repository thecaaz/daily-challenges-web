using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services;
using DailyChallenges.Services.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DailyChallenges.IntegrationTests;

/// <summary>
/// Finalization against the real DbContext, repositories and migrations (H6, B5, B6).
/// Each test gets its own factory, so its own database.
/// </summary>
public class ScoringDayFinalizationTests
{
    private static readonly DateTime Today = FixedClockApiFactory.Now.Date; // reset 00:00 → current day
    private static readonly DateTime Yesterday = Today.AddDays(-1);
    private const int WinXp = 100;

    private sealed record Seed(int GameId, int WinnerId, int RunnerUpId);

    private static async Task<Seed> SeedDayAsync(FixedClockApiFactory f, params DateTime[] days)
    {
        Seed seed = null!;
        await f.WithDbAsync(async db =>
        {
            var game = new Game { Name = "Fin", ResetTime = TimeSpan.Zero, RankingMode = RankingMode.Highest };
            var winner = new User { Username = "winner", PasswordHash = "x" };
            var runnerUp = new User { Username = "runner-up", PasswordHash = "x" };
            db.AddRange(game, winner, runnerUp);
            await db.SaveChangesAsync();

            foreach (var day in days)
            {
                db.Submissions.Add(Sub(game.Id, winner, 200, day.AddHours(8)));
                db.Submissions.Add(Sub(game.Id, runnerUp, 100, day.AddHours(9)));
            }
            await db.SaveChangesAsync();
            seed = new Seed(game.Id, winner.Id, runnerUp.Id);
        });
        return seed;
    }

    private static Submission Sub(int gameId, User user, double value, DateTime createdAt) => new()
    {
        GameId = gameId,
        UserId = user.Id,
        Username = user.Username,
        Score = value.ToString(),
        ScoreValue = value,
        CreatedAt = createdAt,
        ScoringDay = createdAt.Date,
        ScreenshotData = new byte[] { 1 },
        ScreenshotContentType = "image/png",
    };

    private static async Task FinalizeAsync(FixedClockApiFactory f, int gameId, DateTime day)
    {
        using var scope = f.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IScoringDayFinalizerService>().FinalizeScoringDayAsync(gameId, day);
    }

    private static async Task<T> QueryAsync<T>(FixedClockApiFactory f, Func<AppDbContext, Task<T>> query)
    {
        T result = default!;
        await f.WithDbAsync(async db => result = await query(db));
        return result;
    }

    [Fact]
    public async Task First_win_unlocks_win_1_immediately()
    {
        using var f = new FixedClockApiFactory();
        var seed = await SeedDayAsync(f, Yesterday);

        await FinalizeAsync(f, seed.GameId, Yesterday);

        var achievements = await QueryAsync(f, db => db.UserAchievements.Where(a => a.UserId == seed.WinnerId).Select(a => a.AchievementId).ToListAsync());
        Assert.Contains("win_1", achievements);
        var result = await QueryAsync(f, db => db.ScoringDayResults.SingleAsync(r => r.GameId == seed.GameId));
        Assert.Equal(seed.WinnerId, result.WinnerUserId);
        Assert.Equal(WinXp, await QueryAsync(f, db => db.Users.Where(u => u.Id == seed.WinnerId).Select(u => u.TotalXp).SingleAsync()));
    }

    [Fact]
    public async Task Finalizing_twice_awards_xp_and_notifies_once()
    {
        using var f = new FixedClockApiFactory();
        var seed = await SeedDayAsync(f, Yesterday);

        await FinalizeAsync(f, seed.GameId, Yesterday);
        await FinalizeAsync(f, seed.GameId, Yesterday);

        Assert.Equal(1, await QueryAsync(f, db => db.XpEvents.CountAsync(e => e.EventType == "day_win")));
        Assert.Equal(WinXp, await QueryAsync(f, db => db.Users.Where(u => u.Id == seed.WinnerId).Select(u => u.TotalXp).SingleAsync()));
        Assert.Equal(2, await QueryAsync(f, db => db.Notifications.CountAsync(n => n.GameId == seed.GameId)));
        Assert.Equal(1, await QueryAsync(f, db => db.ScoringDayResults.CountAsync()));
    }

    [Fact]
    public async Task Day_win_award_is_idempotent_even_without_the_result_row()
    {
        // E.g. a result row removed by hand: re-finalizing must not pay the win twice.
        using var f = new FixedClockApiFactory();
        var seed = await SeedDayAsync(f, Yesterday);
        await FinalizeAsync(f, seed.GameId, Yesterday);
        await f.WithDbAsync(db => db.ScoringDayResults.ExecuteDeleteAsync());

        await FinalizeAsync(f, seed.GameId, Yesterday);

        Assert.Equal(1, await QueryAsync(f, db => db.XpEvents.CountAsync(e => e.EventType == "day_win")));
        Assert.Equal(WinXp, await QueryAsync(f, db => db.Users.Where(u => u.Id == seed.WinnerId).Select(u => u.TotalXp).SingleAsync()));
    }

    /// <summary>Fails when the finalizer writes notifications (after the claim and the win XP).</summary>
    private sealed class ThrowingNotificationRepository : INotificationRepository
    {
        public Task CreateBatchAsync(List<Notification> notifications) => throw new InvalidOperationException("boom");
        public Task<(List<Notification> Items, int TotalCount, int UnreadCount)> GetByUserPagedAsync(int userId, int page, int pageSize) => throw new NotSupportedException();
        public Task<int> GetUnreadCountAsync(int userId) => throw new NotSupportedException();
        public Task MarkReadAsync(int id, int userId) => throw new NotSupportedException();
        public Task MarkAllReadAsync(int userId) => throw new NotSupportedException();
        public Task<Notification?> GetByIdAsync(int id) => throw new NotSupportedException();
        public Task DeleteAsync(int id) => throw new NotSupportedException();
        public Task<int> DeleteDayResultNotificationsAsync(int gameId, DateTime scoringDay) => throw new NotSupportedException();
        public Task<bool> HasRecentUnreadFromActorAsync(int userId, string type, int actorUserId, DateTime sinceUtc) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Exception_mid_finalize_rolls_everything_back_and_a_retry_succeeds()
    {
        using var f = new FixedClockApiFactory();
        var seed = await SeedDayAsync(f, Yesterday);

        using (var scope = f.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var finalizer = new ScoringDayFinalizerService(
                sp.GetRequiredService<AppDbContext>(),
                sp.GetRequiredService<IScoringDayResultRepository>(),
                sp.GetRequiredService<ISubmissionRepository>(),
                sp.GetRequiredService<IGameRepository>(),
                new ThrowingNotificationRepository(),
                sp.GetRequiredService<IXpService>(),
                sp.GetRequiredService<IAchievementService>(),
                sp.GetRequiredService<IOptions<XpConfig>>(),
                NullLogger<ScoringDayFinalizerService>.Instance,
                f.Clock);

            await Assert.ThrowsAsync<InvalidOperationException>(() => finalizer.FinalizeScoringDayAsync(seed.GameId, Yesterday));
        }

        Assert.Equal(0, await QueryAsync(f, db => db.ScoringDayResults.CountAsync()));
        Assert.Equal(0, await QueryAsync(f, db => db.XpEvents.CountAsync()));
        Assert.Equal(0L, await QueryAsync(f, db => db.Users.Where(u => u.Id == seed.WinnerId).Select(u => u.TotalXp).SingleAsync()));
        Assert.Equal(0, await QueryAsync(f, db => db.UserAchievements.CountAsync()));

        await FinalizeAsync(f, seed.GameId, Yesterday);

        Assert.Equal(1, await QueryAsync(f, db => db.ScoringDayResults.CountAsync()));
        Assert.Equal(WinXp, await QueryAsync(f, db => db.Users.Where(u => u.Id == seed.WinnerId).Select(u => u.TotalXp).SingleAsync()));
    }

    [Fact]
    public async Task Day_win_xp_can_unlock_a_level_achievement()
    {
        using var f = new FixedClockApiFactory();
        var seed = await SeedDayAsync(f, Yesterday);
        // 1 703 XP enters level 5 with the default curve; the win's 100 XP crosses it.
        await f.WithDbAsync(db => db.Users.Where(u => u.Id == seed.WinnerId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.TotalXp, 1650L).SetProperty(u => u.Level, 4)));

        await FinalizeAsync(f, seed.GameId, Yesterday);

        var user = await QueryAsync(f, db => db.Users.AsNoTracking().SingleAsync(u => u.Id == seed.WinnerId));
        Assert.Equal(5, user.Level);
        Assert.Contains("level_5", await QueryAsync(f, db => db.UserAchievements.Where(a => a.UserId == seed.WinnerId).Select(a => a.AchievementId).ToListAsync()));
    }

    // ── Background service catch-up ───────────────────────────────────────────

    private static ScoringDayBackgroundService Background(FixedClockApiFactory f) =>
        new(f.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<ScoringDayBackgroundService>.Instance, f.Clock);

    [Fact]
    public async Task Background_tick_catches_up_three_missed_days()
    {
        using var f = new FixedClockApiFactory();
        var seed = await SeedDayAsync(f, Today.AddDays(-3), Today.AddDays(-2), Yesterday, Today);

        await Background(f).ProcessAllGamesAsync();

        var days = await QueryAsync(f, db => db.ScoringDayResults.Where(r => r.GameId == seed.GameId).Select(r => r.ScoringDay).OrderBy(d => d).ToListAsync());
        Assert.Equal(new[] { Today.AddDays(-3), Today.AddDays(-2), Yesterday }, days.Select(d => d.Date));
        Assert.Equal(3 * WinXp, await QueryAsync(f, db => db.Users.Where(u => u.Id == seed.WinnerId).Select(u => u.TotalXp).SingleAsync()));

        // A second tick has nothing left to do; the current day stays open.
        await Background(f).ProcessAllGamesAsync();
        Assert.Equal(3, await QueryAsync(f, db => db.ScoringDayResults.CountAsync()));
    }

    [Fact]
    public async Task Background_tick_fills_an_unfinalized_gap_day_before_the_last_finalized_day()
    {
        using var f = new FixedClockApiFactory();
        var seed = await SeedDayAsync(f, Today.AddDays(-3), Today.AddDays(-2), Yesterday);
        // -3 and -1 are finalized, -2 is not (e.g. un-finalized by an admin edit whose re-finalization failed).
        await FinalizeAsync(f, seed.GameId, Today.AddDays(-3));
        await FinalizeAsync(f, seed.GameId, Yesterday);

        await Background(f).ProcessAllGamesAsync();

        var days = await QueryAsync(f, db => db.ScoringDayResults.Where(r => r.GameId == seed.GameId).Select(r => r.ScoringDay).OrderBy(d => d).ToListAsync());
        Assert.Equal(new[] { Today.AddDays(-3), Today.AddDays(-2), Yesterday }, days.Select(d => d.Date));
        Assert.Equal(3 * WinXp, await QueryAsync(f, db => db.Users.Where(u => u.Id == seed.WinnerId).Select(u => u.TotalXp).SingleAsync()));

        // Idempotent: another tick changes nothing.
        await Background(f).ProcessAllGamesAsync();
        Assert.Equal(3, await QueryAsync(f, db => db.ScoringDayResults.CountAsync()));
        Assert.Equal(3 * WinXp, await QueryAsync(f, db => db.Users.Where(u => u.Id == seed.WinnerId).Select(u => u.TotalXp).SingleAsync()));
    }

    [Fact]
    public async Task Background_tick_fills_gaps_older_than_the_cap_oldest_first_and_is_capped()
    {
        using var f = new FixedClockApiFactory();
        var seed = await SeedDayAsync(f, Today.AddDays(-30), Yesterday);
        await f.WithDbAsync(async db =>
        {
            db.ScoringDayResults.Add(new ScoringDayResult { GameId = seed.GameId, ScoringDay = Today.AddDays(-20), ProcessedAt = Today });
            await db.SaveChangesAsync();
        });

        // Open: -30 .. -21 (10 days, before the last finalized day) and -19 .. -1 (19 days) = 29.
        // The first tick does the oldest 14: -30 .. -21 and -19 .. -16.
        await Background(f).ProcessAllGamesAsync();
        Assert.Equal(1 + ScoringDayBackgroundService.MaxDaysPerTick, await QueryAsync(f, db => db.ScoringDayResults.CountAsync()));
        Assert.True(await QueryAsync(f, db => db.ScoringDayResults.AnyAsync(r => r.ScoringDay == Today.AddDays(-30))));
        Assert.True(await QueryAsync(f, db => db.ScoringDayResults.AnyAsync(r => r.ScoringDay == Today.AddDays(-16))));
        Assert.False(await QueryAsync(f, db => db.ScoringDayResults.AnyAsync(r => r.ScoringDay == Today.AddDays(-15))));

        await Background(f).ProcessAllGamesAsync();
        Assert.Equal(1 + 2 * ScoringDayBackgroundService.MaxDaysPerTick, await QueryAsync(f, db => db.ScoringDayResults.CountAsync()));

        await Background(f).ProcessAllGamesAsync();
        var last = await QueryAsync(f, db => db.ScoringDayResults.MaxAsync(r => r.ScoringDay));
        Assert.Equal(Yesterday, last.Date);
        Assert.Equal(1 + 29, await QueryAsync(f, db => db.ScoringDayResults.CountAsync()));
        // Two days had submissions (-30 and yesterday): two wins.
        Assert.Equal(2 * WinXp, await QueryAsync(f, db => db.Users.Where(u => u.Id == seed.WinnerId).Select(u => u.TotalXp).SingleAsync()));
    }

    [Fact]
    public async Task Background_tick_retries_gaps_within_the_scan_window_but_not_beyond_it()
    {
        using var f = new FixedClockApiFactory();
        var oldGap = Today.AddDays(-(ScoringDayBackgroundService.GapScanDays + 5));
        var recentGap = Today.AddDays(-40);
        var seed = await SeedDayAsync(f, Today.AddDays(-100), oldGap, recentGap, Yesterday);
        await f.WithDbAsync(async db =>
        {
            for (var day = Today.AddDays(-100); day < Today; day = day.AddDays(1))
                if (day != oldGap && day != recentGap)
                    db.ScoringDayResults.Add(new ScoringDayResult { GameId = seed.GameId, ScoringDay = day, ProcessedAt = Today });
            await db.SaveChangesAsync();
        });

        await Background(f).ProcessAllGamesAsync();

        Assert.True(await QueryAsync(f, db => db.ScoringDayResults.AnyAsync(r => r.ScoringDay == recentGap)));
        Assert.False(await QueryAsync(f, db => db.ScoringDayResults.AnyAsync(r => r.ScoringDay == oldGap)));
        Assert.Equal(WinXp, await QueryAsync(f, db => db.Users.Where(u => u.Id == seed.WinnerId).Select(u => u.TotalXp).SingleAsync()));
    }
}
