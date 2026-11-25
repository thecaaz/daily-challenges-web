using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;

namespace DailyChallenges.Services;

/// <summary>
/// Finalizes ended scoring days every <see cref="TickInterval"/> and, at most hourly, purges expired revoked
/// refresh tokens. This is a boundary: a failure is logged and retried on the next tick instead of
/// stopping the service. Every game and every day gets its own DI scope (and DbContext), so one failure
/// cannot poison the others.
/// </summary>
public sealed class ScoringDayBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ScoringDayBackgroundService> logger,
    TimeProvider time) : BackgroundService
{
    /// <summary>At most this many days are finalized per game per tick, so a long outage catches up gradually.</summary>
    public const int MaxDaysPerTick = 14;

    /// <summary>
    /// How far back every tick looks for un-finalized ended days (e.g. a day un-finalized by an admin
    /// edit/delete whose re-finalization failed). Never before the game's first submission day.
    /// </summary>
    public const int GapScanDays = 90;

    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RevokedTokenPurgeInterval = TimeSpan.FromHours(1);
    private DateTimeOffset _lastRevokedTokenPurge = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ScoringDayBackgroundService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunFinalizationTickAsync(stoppingToken);
            await PurgeRevokedTokensIfDueAsync(stoppingToken);
            if (!await WaitForNextTickAsync(stoppingToken)) break;
        }
    }

    /// <summary>
    /// One tick: for every game, finalize each ended scoring day that is not finalized yet, oldest first
    /// (at most <see cref="MaxDaysPerTick"/> per game, within <see cref="ScoringDayCatchUpWindow"/>).
    /// </summary>
    public async Task ProcessAllGamesAsync(CancellationToken ct = default)
    {
        foreach (var game in await GetGamesAsync())
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                await FinalizeEndedDaysAsync(game.Id, game.ResetTime, ct);
            }
            catch (Exception ex) when (!IsShutdown(ex, ct))
            {
                logger.LogError(ex, "Error finalizing scoring days for game {GameId} ({GameName})", game.Id, game.Name);
            }
        }
    }

    /// <summary>Deletes revoked refresh tokens that have expired anyway (they can't be replayed any more).</summary>
    public async Task<int> PurgeExpiredRevokedTokensAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var sessions = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
        var purged = await sessions.DeleteExpiredRevocationsAsync(time.GetUtcNow().UtcDateTime, ct);
        if (purged > 0)
            logger.LogInformation("Purged {Count} expired revoked refresh tokens", purged);
        return purged;
    }

    private async Task RunFinalizationTickAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ProcessAllGamesAsync(stoppingToken);
        }
        catch (Exception ex) when (!IsShutdown(ex, stoppingToken))
        {
            logger.LogError(ex, "Unhandled error in ScoringDayBackgroundService tick");
        }
    }

    private async Task PurgeRevokedTokensIfDueAsync(CancellationToken stoppingToken)
    {
        if (time.GetUtcNow() - _lastRevokedTokenPurge < RevokedTokenPurgeInterval) return;

        try
        {
            await PurgeExpiredRevokedTokensAsync(stoppingToken);
            _lastRevokedTokenPurge = time.GetUtcNow();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Error purging expired revoked refresh tokens");
        }
    }

    /// <returns>False when the service is stopping.</returns>
    private async Task<bool> WaitForNextTickAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TickInterval, time, stoppingToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task<List<(int Id, string Name, TimeSpan ResetTime)>> GetGamesAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var games = await scope.ServiceProvider.GetRequiredService<IGameRepository>().GetAllAsync();
        return games.Select(g => (g.Id, g.Name, g.ResetTime)).ToList();
    }

    /// <summary>
    /// Stops at the first failing day so days are finalized in order; the next tick retries it.
    /// The finalizer is idempotent, so a day raced by another finalization is harmless.
    /// </summary>
    private async Task FinalizeEndedDaysAsync(int gameId, TimeSpan resetTime, CancellationToken ct)
    {
        var currentDay = ScoringDayCalculator.GetCurrentScoringDay(resetTime, time.GetUtcNow().UtcDateTime).Date;
        var window = await GetCatchUpWindowAsync(gameId, currentDay);
        if (window == null) return;

        foreach (var day in await GetDaysToFinalizeAsync(gameId, window))
        {
            if (ct.IsCancellationRequested) break;
            if (!await TryFinalizeDayAsync(gameId, day, ct)) break;
        }
    }

    /// <summary>Null when the game has no submissions (nothing to finalize) or no ended day is in the window.</summary>
    private async Task<ScoringDayCatchUpWindow?> GetCatchUpWindowAsync(int gameId, DateTime currentDay)
    {
        using var scope = scopeFactory.CreateScope();
        var lastFinalizedDay = await scope.ServiceProvider.GetRequiredService<IScoringDayResultRepository>().GetLastFinalizedDayAsync(gameId);
        var firstSubmissionDay = await scope.ServiceProvider.GetRequiredService<ISubmissionRepository>().GetFirstScoringDayAsync(gameId);
        if (firstSubmissionDay is not DateTime firstDay) return null;

        return ScoringDayCatchUpWindow.For(currentDay, lastFinalizedDay, firstDay, GapScanDays);
    }

    private async Task<List<DateTime>> GetDaysToFinalizeAsync(int gameId, ScoringDayCatchUpWindow window)
    {
        using var scope = scopeFactory.CreateScope();
        var finalizedDays = await scope.ServiceProvider.GetRequiredService<IScoringDayResultRepository>()
            .GetFinalizedDaysAsync(gameId, window.FirstDay, window.LastDay);
        return window.UnfinalizedDays(finalizedDays.ToHashSet()).Take(MaxDaysPerTick).ToList();
    }

    /// <returns>False when the day failed (the error is logged).</returns>
    private async Task<bool> TryFinalizeDayAsync(int gameId, DateTime day, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IScoringDayFinalizerService>().FinalizeScoringDayAsync(gameId, day);
            return true;
        }
        catch (Exception ex) when (!IsShutdown(ex, ct))
        {
            logger.LogError(ex, "Error finalizing scoring day {Day} for game {GameId}", ScoringDayCalculator.FormatScoringDay(day), gameId);
            return false;
        }
    }

    private static bool IsShutdown(Exception ex, CancellationToken ct) =>
        ex is OperationCanceledException && ct.IsCancellationRequested;
}
