using DailyChallenges.IntegrationTests.Infrastructure;
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

    private static Task<long> WinnerXpAsync(FixedClockApiFactory f, Seed seed) =>
        f.QueryDbAsync(db => db.Users.Where(u => u.Id == seed.WinnerId).Select(u => u.TotalXp).SingleAsync());

    private static Task<int> ResultCountAsync(FixedClockApiFactory f) =>
        f.QueryDbAsync(db => db.ScoringDayResults.CountAsync());

    private static async Task<List<DateTime>> FinalizedDaysAsync(FixedClockApiFactory f, Seed seed)
    {
        var days = await f.QueryDbAsync(db => db.ScoringDayResults.Where(r => r.GameId == seed.GameId).Select(r => r.ScoringDay).OrderBy(d => d).ToListAsync());
        return days.Select(d => d.Date).ToList();
    }

    [Fact]
    public async Task First_win_unlocks_win_1_immediately()
    {
        using var f = new FixedClockApiFactory();
        var seed = await SeedDayAsync(f, Yesterday);

        await f.FinalizeScoringDayAsync(seed.GameId, Yesterday);

        var achievements = await f.QueryDbAsync(db => db.UserAchievements.Where(a => a.UserId == seed.WinnerId).Select(a => a.AchievementId).ToListAsync());
        Assert.Contains("win_1", achievements);
        var result = await f.QueryDbAsync(db => db.ScoringDayResults.SingleAsync(r => r.GameId == seed.GameId));
        Assert.Equal(seed.WinnerId, result.WinnerUserId);
        Assert.Equal(WinXp, await WinnerXpAsync(f, seed));
    }

    [Fact]
    public async Task Finalizing_twice_awards_xp_and_notifies_once()
    {
        using var f = new FixedClockApiFactory();
        var seed = await SeedDayAsync(f, Yesterday);

        await f.FinalizeScoringDayAsync(seed.GameId, Yesterday);
        await f.FinalizeScoringDayAsync(seed.GameId, Yesterday);

        Assert.Equal(1, await f.QueryDbAsync(db => db.XpEvents.CountAsync(e => e.EventType == "day_win")));
        Assert.Equal(WinXp, await WinnerXpAsync(f, seed));
        Assert.Equal(2, await f.QueryDbAsync(db => db.Notifications.CountAsync(n => n.GameId == seed.GameId)));
        Assert.Equal(1, await ResultCountAsync(f));
    }

    /// <summary>E.g. a result row removed by hand: re-finalizing must not pay the win twice.</summary>
    [Fact]
    public async Task Day_win_award_is_idempotent_even_without_the_result_row()
    {
        using var f = new FixedClockApiFactory();
        var seed = await SeedDayAsync(f, Yesterday);
        await f.FinalizeScoringDayAsync(seed.GameId, Yesterday);
        await f.WithDbAsync(db => db.ScoringDayResults.ExecuteDeleteAsync());

        await f.FinalizeScoringDayAsync(seed.GameId, Yesterday);

        Assert.Equal(1, await f.QueryDbAsync(db => db.XpEvents.CountAsync(e => e.EventType == "day_win")));
        Assert.Equal(WinXp, await WinnerXpAsync(f, seed));
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
        public Task<bool> HasRecentUnreadFromActorAsync(RecentNotificationQuery query) => throw new NotSupportedException();
    }

    /// <summary>Finalizes yesterday with a finalizer whose notification step throws, and expects the exception.</summary>
    private static async Task FinalizeWithFailingNotificationsAsync(FixedClockApiFactory f, Seed seed)
    {
        using var scope = f.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var recorder = new ScoringDayOutcomeRecorder(
            sp.GetRequiredService<ISubmissionRepository>(),
            sp.GetRequiredService<IScoringDayResultRepository>(),
            sp.GetRequiredService<IXpService>(),
            new ThrowingNotificationRepository(),
            sp.GetRequiredService<ScoringDayNotificationFactory>(),
            f.Clock,
            NullLogger<ScoringDayOutcomeRecorder>.Instance);
        var finalizer = new ScoringDayFinalizerService(
            sp.GetRequiredService<IScoringDayResultRepository>(),
            sp.GetRequiredService<IGameRepository>(),
            recorder,
            sp.GetRequiredService<ITransactionRunner>(),
            sp.GetRequiredService<IAchievementService>(),
            NullLogger<ScoringDayFinalizerService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => finalizer.FinalizeScoringDayAsync(seed.GameId, Yesterday, CancellationToken.None));
    }

    [Fact]
    public async Task Exception_mid_finalize_rolls_everything_back()
    {
        using var f = new FixedClockApiFactory();
        var seed = await SeedDayAsync(f, Yesterday);

        await FinalizeWithFailingNotificationsAsync(f, seed);

        Assert.Equal(0, await ResultCountAsync(f));
        Assert.Equal(0, await f.QueryDbAsync(db => db.XpEvents.CountAsync()));
        Assert.Equal(0L, await WinnerXpAsync(f, seed));
        Assert.Equal(0, await f.QueryDbAsync(db => db.UserAchievements.CountAsync()));
    }

    [Fact]
    public async Task Finalizing_again_after_a_rolled_back_attempt_succeeds()
    {
        using var f = new FixedClockApiFactory();
        var seed = await SeedDayAsync(f, Yesterday);
        await FinalizeWithFailingNotificationsAsync(f, seed);

        await f.FinalizeScoringDayAsync(seed.GameId, Yesterday);

        Assert.Equal(1, await ResultCountAsync(f));
        Assert.Equal(WinXp, await WinnerXpAsync(f, seed));
    }

    /// <summary>1 703 XP enters level 5 with the default curve; the win's 100 XP crosses it.</summary>
    [Fact]
    public async Task Day_win_xp_can_unlock_a_level_achievement()
    {
        using var f = new FixedClockApiFactory();
        var seed = await SeedDayAsync(f, Yesterday);
        const long level4XpWithinOneWinOfLevel5 = 1650;
        await f.WithDbAsync(db => db.Users.Where(u => u.Id == seed.WinnerId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.TotalXp, level4XpWithinOneWinOfLevel5).SetProperty(u => u.Level, 4)));

        await f.FinalizeScoringDayAsync(seed.GameId, Yesterday);

        var user = await f.QueryDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == seed.WinnerId));
        Assert.Equal(5, user.Level);
        Assert.Contains("level_5", await f.QueryDbAsync(db => db.UserAchievements.Where(a => a.UserId == seed.WinnerId).Select(a => a.AchievementId).ToListAsync()));
    }

    // ── Background service catch-up ───────────────────────────────────────────

    private static ScoringDayBackgroundService Background(FixedClockApiFactory f) =>
        new(f.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<ScoringDayBackgroundService>.Instance, f.Clock);

    private static Task<Seed> SeedThreeMissedDaysAndTodayAsync(FixedClockApiFactory f) =>
        SeedDayAsync(f, Today.AddDays(-3), Today.AddDays(-2), Yesterday, Today);

    [Fact]
    public async Task Background_tick_catches_up_three_missed_days()
    {
        using var f = new FixedClockApiFactory();
        var seed = await SeedThreeMissedDaysAndTodayAsync(f);

        await Background(f).ProcessAllGamesAsync(CancellationToken.None);

        Assert.Equal(new[] { Today.AddDays(-3), Today.AddDays(-2), Yesterday }, await FinalizedDaysAsync(f, seed));
        Assert.Equal(3 * WinXp, await WinnerXpAsync(f, seed));
    }

    [Fact]
    public async Task Background_tick_after_catching_up_leaves_the_current_day_open()
    {
        using var f = new FixedClockApiFactory();
        await SeedThreeMissedDaysAndTodayAsync(f);
        await Background(f).ProcessAllGamesAsync(CancellationToken.None);

        await Background(f).ProcessAllGamesAsync(CancellationToken.None);

        Assert.Equal(3, await ResultCountAsync(f));
    }

    /// <summary>
    /// Days -3 and -1 are finalized but -2 is not (e.g. un-finalized by an admin edit whose re-finalization failed).
    /// </summary>
    private static async Task<Seed> SeedUnfinalizedGapDayAsync(FixedClockApiFactory f)
    {
        var seed = await SeedDayAsync(f, Today.AddDays(-3), Today.AddDays(-2), Yesterday);
        await f.FinalizeScoringDayAsync(seed.GameId, Today.AddDays(-3));
        await f.FinalizeScoringDayAsync(seed.GameId, Yesterday);
        return seed;
    }

    [Fact]
    public async Task Background_tick_fills_an_unfinalized_gap_day_before_the_last_finalized_day()
    {
        using var f = new FixedClockApiFactory();
        var seed = await SeedUnfinalizedGapDayAsync(f);

        await Background(f).ProcessAllGamesAsync(CancellationToken.None);

        Assert.Equal(new[] { Today.AddDays(-3), Today.AddDays(-2), Yesterday }, await FinalizedDaysAsync(f, seed));
        Assert.Equal(3 * WinXp, await WinnerXpAsync(f, seed));
    }

    [Fact]
    public async Task Background_tick_after_filling_a_gap_changes_nothing()
    {
        using var f = new FixedClockApiFactory();
        var seed = await SeedUnfinalizedGapDayAsync(f);
        await Background(f).ProcessAllGamesAsync(CancellationToken.None);

        await Background(f).ProcessAllGamesAsync(CancellationToken.None);

        Assert.Equal(3, await ResultCountAsync(f));
        Assert.Equal(3 * WinXp, await WinnerXpAsync(f, seed));
    }

    private const int OpenDaysAroundTheLoneResult = 29;

    /// <summary>
    /// Submissions on day -30 and yesterday, and a lone result for day -20. Open days: -30 .. -21 (10 days, before
    /// the last finalized day) and -19 .. -1 (19 days), <see cref="OpenDaysAroundTheLoneResult"/> in all.
    /// </summary>
    private static async Task<Seed> SeedOpenDaysAroundALoneResultAsync(FixedClockApiFactory f)
    {
        var seed = await SeedDayAsync(f, Today.AddDays(-30), Yesterday);
        await f.WithDbAsync(async db =>
        {
            db.ScoringDayResults.Add(new ScoringDayResult { GameId = seed.GameId, ScoringDay = Today.AddDays(-20), ProcessedAt = Today });
            await db.SaveChangesAsync();
        });
        return seed;
    }

    /// <summary>The first tick does the oldest 14 open days: -30 .. -21 and -19 .. -16.</summary>
    [Fact]
    public async Task Background_tick_finalizes_at_most_the_cap_oldest_first()
    {
        using var f = new FixedClockApiFactory();
        await SeedOpenDaysAroundALoneResultAsync(f);
        const int loneResult = 1;

        await Background(f).ProcessAllGamesAsync(CancellationToken.None);

        Assert.Equal(loneResult + ScoringDayBackgroundService.MaxDaysPerTick, await ResultCountAsync(f));
        Assert.True(await f.QueryDbAsync(db => db.ScoringDayResults.AnyAsync(r => r.ScoringDay == Today.AddDays(-30))));
        Assert.True(await f.QueryDbAsync(db => db.ScoringDayResults.AnyAsync(r => r.ScoringDay == Today.AddDays(-16))));
        Assert.False(await f.QueryDbAsync(db => db.ScoringDayResults.AnyAsync(r => r.ScoringDay == Today.AddDays(-15))));

        await Background(f).ProcessAllGamesAsync(CancellationToken.None);

        Assert.Equal(loneResult + 2 * ScoringDayBackgroundService.MaxDaysPerTick, await ResultCountAsync(f));
    }

    [Fact]
    public async Task Background_ticks_eventually_finalize_every_open_day_and_pay_only_days_with_submissions()
    {
        using var f = new FixedClockApiFactory();
        var seed = await SeedOpenDaysAroundALoneResultAsync(f);
        const int loneResult = 1;
        const int daysWithSubmissions = 2;
        const int ticksToFinalizeEveryOpenDay = 3;

        for (var tick = 0; tick < ticksToFinalizeEveryOpenDay; tick++)
            await Background(f).ProcessAllGamesAsync(CancellationToken.None);

        var last = await f.QueryDbAsync(db => db.ScoringDayResults.MaxAsync(r => r.ScoringDay));
        Assert.Equal(Yesterday, last.Date);
        Assert.Equal(loneResult + OpenDaysAroundTheLoneResult, await ResultCountAsync(f));
        Assert.Equal(daysWithSubmissions * WinXp, await WinnerXpAsync(f, seed));
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

        await Background(f).ProcessAllGamesAsync(CancellationToken.None);

        Assert.True(await f.QueryDbAsync(db => db.ScoringDayResults.AnyAsync(r => r.ScoringDay == recentGap)));
        Assert.False(await f.QueryDbAsync(db => db.ScoringDayResults.AnyAsync(r => r.ScoringDay == oldGap)));
        Assert.Equal(WinXp, await WinnerXpAsync(f, seed));
    }
}
