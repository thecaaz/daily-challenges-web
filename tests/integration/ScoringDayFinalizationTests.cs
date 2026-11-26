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

    private static async Task<Seed> SeedDayAsync(FixedClockApiFactory factory, params DateTime[] days)
    {
        Seed seed = null!;
        await factory.WithDbAsync(async db =>
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

    private static Task<long> WinnerXpAsync(FixedClockApiFactory factory, Seed seed) =>
        factory.QueryDbAsync(db => db.Users.Where(u => u.Id == seed.WinnerId).Select(u => u.TotalXp).SingleAsync());

    private static Task<int> ResultCountAsync(FixedClockApiFactory factory) =>
        factory.QueryDbAsync(db => db.ScoringDayResults.CountAsync());

    private static async Task<List<DateTime>> FinalizedDaysAsync(FixedClockApiFactory factory, Seed seed)
    {
        var days = await factory.QueryDbAsync(db => db.ScoringDayResults.Where(r => r.GameId == seed.GameId).Select(r => r.ScoringDay).OrderBy(d => d).ToListAsync());
        return days.Select(d => d.Date).ToList();
    }

    [Fact]
    public async Task First_win_unlocks_win_1_immediately()
    {
        using var factory = new FixedClockApiFactory();
        var seed = await SeedDayAsync(factory, Yesterday);

        await factory.FinalizeScoringDayAsync(seed.GameId, Yesterday);

        var achievements = await factory.QueryDbAsync(db => db.UserAchievements.Where(a => a.UserId == seed.WinnerId).Select(a => a.AchievementId).ToListAsync());
        Assert.Contains("win_1", achievements);
        var result = await factory.QueryDbAsync(db => db.ScoringDayResults.SingleAsync(r => r.GameId == seed.GameId));
        Assert.Equal(seed.WinnerId, result.WinnerUserId);
        Assert.Equal(WinXp, await WinnerXpAsync(factory, seed));
    }

    [Fact]
    public async Task Finalizing_twice_awards_xp_and_notifies_once()
    {
        using var factory = new FixedClockApiFactory();
        var seed = await SeedDayAsync(factory, Yesterday);

        await factory.FinalizeScoringDayAsync(seed.GameId, Yesterday);
        await factory.FinalizeScoringDayAsync(seed.GameId, Yesterday);

        Assert.Equal(1, await factory.QueryDbAsync(db => db.XpEvents.CountAsync(e => e.EventType == "day_win")));
        Assert.Equal(WinXp, await WinnerXpAsync(factory, seed));
        Assert.Equal(2, await factory.QueryDbAsync(db => db.Notifications.CountAsync(n => n.GameId == seed.GameId)));
        Assert.Equal(1, await ResultCountAsync(factory));
    }

    /// <summary>E.g. a result row removed by hand: re-finalizing must not pay the win twice.</summary>
    [Fact]
    public async Task Day_win_award_is_idempotent_even_without_the_result_row()
    {
        using var factory = new FixedClockApiFactory();
        var seed = await SeedDayAsync(factory, Yesterday);
        await factory.FinalizeScoringDayAsync(seed.GameId, Yesterday);
        await factory.WithDbAsync(db => db.ScoringDayResults.ExecuteDeleteAsync());

        await factory.FinalizeScoringDayAsync(seed.GameId, Yesterday);

        Assert.Equal(1, await factory.QueryDbAsync(db => db.XpEvents.CountAsync(e => e.EventType == "day_win")));
        Assert.Equal(WinXp, await WinnerXpAsync(factory, seed));
    }

    /// <summary>Fails when the finalizer writes notifications (after the claim and the win XP).</summary>
    private sealed class ThrowingNotificationRepository : INotificationRepository
    {
        public Task CreateBatchAsync(List<Notification> notifications, CancellationToken ct) => throw new InvalidOperationException("boom");
        public Task<(List<Notification> Items, int TotalCount, int UnreadCount)> GetByUserPagedAsync(int userId, int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> GetUnreadCountAsync(int userId, CancellationToken ct) => throw new NotSupportedException();
        public Task MarkReadAsync(int id, int userId, CancellationToken ct) => throw new NotSupportedException();
        public Task MarkAllReadAsync(int userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Notification?> GetByIdAsync(int id, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(int id, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> DeleteDayResultNotificationsAsync(int gameId, DateTime scoringDay, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasRecentUnreadFromActorAsync(RecentNotificationQuery query, CancellationToken ct) => throw new NotSupportedException();
    }

    /// <summary>Finalizes yesterday with a finalizer whose notification step throws, and expects the exception.</summary>
    private static async Task FinalizeWithFailingNotificationsAsync(FixedClockApiFactory factory, Seed seed)
    {
        using var scope = factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var recorder = new ScoringDayOutcomeRecorder(
            sp.GetRequiredService<ISubmissionRepository>(),
            sp.GetRequiredService<IScoringDayResultRepository>(),
            sp.GetRequiredService<IXpService>(),
            new ThrowingNotificationRepository(),
            sp.GetRequiredService<ScoringDayNotificationFactory>(),
            factory.Clock,
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
        using var factory = new FixedClockApiFactory();
        var seed = await SeedDayAsync(factory, Yesterday);

        await FinalizeWithFailingNotificationsAsync(factory, seed);

        Assert.Equal(0, await ResultCountAsync(factory));
        Assert.Equal(0, await factory.QueryDbAsync(db => db.XpEvents.CountAsync()));
        Assert.Equal(0L, await WinnerXpAsync(factory, seed));
        Assert.Equal(0, await factory.QueryDbAsync(db => db.UserAchievements.CountAsync()));
    }

    [Fact]
    public async Task Finalizing_again_after_a_rolled_back_attempt_succeeds()
    {
        using var factory = new FixedClockApiFactory();
        var seed = await SeedDayAsync(factory, Yesterday);
        await FinalizeWithFailingNotificationsAsync(factory, seed);

        await factory.FinalizeScoringDayAsync(seed.GameId, Yesterday);

        Assert.Equal(1, await ResultCountAsync(factory));
        Assert.Equal(WinXp, await WinnerXpAsync(factory, seed));
    }

    /// <summary>1 703 XP enters level 5 with the default curve; the win's 100 XP crosses it.</summary>
    [Fact]
    public async Task Day_win_xp_can_unlock_a_level_achievement()
    {
        using var factory = new FixedClockApiFactory();
        var seed = await SeedDayAsync(factory, Yesterday);
        const long level4XpWithinOneWinOfLevel5 = 1650;
        await factory.WithDbAsync(db => db.Users.Where(u => u.Id == seed.WinnerId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.TotalXp, level4XpWithinOneWinOfLevel5).SetProperty(u => u.Level, 4)));

        await factory.FinalizeScoringDayAsync(seed.GameId, Yesterday);

        var user = await factory.QueryDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == seed.WinnerId));
        Assert.Equal(5, user.Level);
        Assert.Contains("level_5", await factory.QueryDbAsync(db => db.UserAchievements.Where(a => a.UserId == seed.WinnerId).Select(a => a.AchievementId).ToListAsync()));
    }

    // ── Background service catch-up ───────────────────────────────────────────

    private static ScoringDayBackgroundService Background(FixedClockApiFactory factory) =>
        new(factory.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<ScoringDayBackgroundService>.Instance, factory.Clock);

    private static Task<Seed> SeedThreeMissedDaysAndTodayAsync(FixedClockApiFactory factory) =>
        SeedDayAsync(factory, Today.AddDays(-3), Today.AddDays(-2), Yesterday, Today);

    [Fact]
    public async Task Background_tick_catches_up_three_missed_days()
    {
        using var factory = new FixedClockApiFactory();
        var seed = await SeedThreeMissedDaysAndTodayAsync(factory);

        await Background(factory).ProcessAllGamesAsync(CancellationToken.None);

        Assert.Equal(new[] { Today.AddDays(-3), Today.AddDays(-2), Yesterday }, await FinalizedDaysAsync(factory, seed));
        Assert.Equal(3 * WinXp, await WinnerXpAsync(factory, seed));
    }

    [Fact]
    public async Task Background_tick_after_catching_up_leaves_the_current_day_open()
    {
        using var factory = new FixedClockApiFactory();
        await SeedThreeMissedDaysAndTodayAsync(factory);
        await Background(factory).ProcessAllGamesAsync(CancellationToken.None);

        await Background(factory).ProcessAllGamesAsync(CancellationToken.None);

        Assert.Equal(3, await ResultCountAsync(factory));
    }

    /// <summary>
    /// Days -3 and -1 are finalized but -2 is not (e.g. un-finalized by an admin edit whose re-finalization failed).
    /// </summary>
    private static async Task<Seed> SeedUnfinalizedGapDayAsync(FixedClockApiFactory factory)
    {
        var seed = await SeedDayAsync(factory, Today.AddDays(-3), Today.AddDays(-2), Yesterday);
        await factory.FinalizeScoringDayAsync(seed.GameId, Today.AddDays(-3));
        await factory.FinalizeScoringDayAsync(seed.GameId, Yesterday);
        return seed;
    }

    [Fact]
    public async Task Background_tick_fills_an_unfinalized_gap_day_before_the_last_finalized_day()
    {
        using var factory = new FixedClockApiFactory();
        var seed = await SeedUnfinalizedGapDayAsync(factory);

        await Background(factory).ProcessAllGamesAsync(CancellationToken.None);

        Assert.Equal(new[] { Today.AddDays(-3), Today.AddDays(-2), Yesterday }, await FinalizedDaysAsync(factory, seed));
        Assert.Equal(3 * WinXp, await WinnerXpAsync(factory, seed));
    }

    [Fact]
    public async Task Background_tick_after_filling_a_gap_changes_nothing()
    {
        using var factory = new FixedClockApiFactory();
        var seed = await SeedUnfinalizedGapDayAsync(factory);
        await Background(factory).ProcessAllGamesAsync(CancellationToken.None);

        await Background(factory).ProcessAllGamesAsync(CancellationToken.None);

        Assert.Equal(3, await ResultCountAsync(factory));
        Assert.Equal(3 * WinXp, await WinnerXpAsync(factory, seed));
    }

    private const int OpenDaysAroundTheLoneResult = 29;

    /// <summary>
    /// Submissions on day -30 and yesterday, and a lone result for day -20. Open days: -30 .. -21 (10 days, before
    /// the last finalized day) and -19 .. -1 (19 days), <see cref="OpenDaysAroundTheLoneResult"/> in all.
    /// </summary>
    private static async Task<Seed> SeedOpenDaysAroundALoneResultAsync(FixedClockApiFactory factory)
    {
        var seed = await SeedDayAsync(factory, Today.AddDays(-30), Yesterday);
        await factory.WithDbAsync(async db =>
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
        using var factory = new FixedClockApiFactory();
        await SeedOpenDaysAroundALoneResultAsync(factory);
        const int loneResult = 1;

        await Background(factory).ProcessAllGamesAsync(CancellationToken.None);

        Assert.Equal(loneResult + ScoringDayBackgroundService.MaxDaysPerTick, await ResultCountAsync(factory));
        Assert.True(await factory.QueryDbAsync(db => db.ScoringDayResults.AnyAsync(r => r.ScoringDay == Today.AddDays(-30))));
        Assert.True(await factory.QueryDbAsync(db => db.ScoringDayResults.AnyAsync(r => r.ScoringDay == Today.AddDays(-16))));
        Assert.False(await factory.QueryDbAsync(db => db.ScoringDayResults.AnyAsync(r => r.ScoringDay == Today.AddDays(-15))));

        await Background(factory).ProcessAllGamesAsync(CancellationToken.None);

        Assert.Equal(loneResult + 2 * ScoringDayBackgroundService.MaxDaysPerTick, await ResultCountAsync(factory));
    }

    [Fact]
    public async Task Background_ticks_eventually_finalize_every_open_day_and_pay_only_days_with_submissions()
    {
        using var factory = new FixedClockApiFactory();
        var seed = await SeedOpenDaysAroundALoneResultAsync(factory);
        const int loneResult = 1;
        const int daysWithSubmissions = 2;
        const int ticksToFinalizeEveryOpenDay = 3;

        for (var tick = 0; tick < ticksToFinalizeEveryOpenDay; tick++)
            await Background(factory).ProcessAllGamesAsync(CancellationToken.None);

        var last = await factory.QueryDbAsync(db => db.ScoringDayResults.MaxAsync(r => r.ScoringDay));
        Assert.Equal(Yesterday, last.Date);
        Assert.Equal(loneResult + OpenDaysAroundTheLoneResult, await ResultCountAsync(factory));
        Assert.Equal(daysWithSubmissions * WinXp, await WinnerXpAsync(factory, seed));
    }

    [Fact]
    public async Task Background_tick_retries_gaps_within_the_scan_window_but_not_beyond_it()
    {
        using var factory = new FixedClockApiFactory();
        var oldGap = Today.AddDays(-(ScoringDayBackgroundService.GapScanDays + 5));
        var recentGap = Today.AddDays(-40);
        var seed = await SeedDayAsync(factory, Today.AddDays(-100), oldGap, recentGap, Yesterday);
        await factory.WithDbAsync(async db =>
        {
            for (var day = Today.AddDays(-100); day < Today; day = day.AddDays(1))
                if (day != oldGap && day != recentGap)
                    db.ScoringDayResults.Add(new ScoringDayResult { GameId = seed.GameId, ScoringDay = day, ProcessedAt = Today });
            await db.SaveChangesAsync();
        });

        await Background(factory).ProcessAllGamesAsync(CancellationToken.None);

        Assert.True(await factory.QueryDbAsync(db => db.ScoringDayResults.AnyAsync(r => r.ScoringDay == recentGap)));
        Assert.False(await factory.QueryDbAsync(db => db.ScoringDayResults.AnyAsync(r => r.ScoringDay == oldGap)));
        Assert.Equal(WinXp, await WinnerXpAsync(factory, seed));
    }
}
