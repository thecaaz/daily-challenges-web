using DailyChallenges.Data;
using DailyChallenges.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Services
{
    public class ScoringDayBackgroundService : BackgroundService
    {
        /// <summary>At most this many days are finalized per game per tick, so a long outage catches up gradually.</summary>
        public const int MaxDaysPerTick = 14;

        /// <summary>
        /// How far back every tick looks for un-finalized ended days (e.g. a day un-finalized by an admin
        /// edit/delete whose re-finalization failed). Never before the game's first submission day.
        /// </summary>
        public const int GapScanDays = 90;

        private readonly TimeProvider _time;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ScoringDayBackgroundService> _logger;
        private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan RevokedTokenPurgeInterval = TimeSpan.FromHours(1);
        private DateTimeOffset _lastRevokedTokenPurge = DateTimeOffset.MinValue;

        public ScoringDayBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ScoringDayBackgroundService> logger, TimeProvider time)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _time = time;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ScoringDayBackgroundService started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAllGamesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error in ScoringDayBackgroundService tick");
                }

                // Cheap housekeeping piggybacking on the tick (at most once per hour).
                if (_time.GetUtcNow() - _lastRevokedTokenPurge >= RevokedTokenPurgeInterval)
                {
                    try
                    {
                        await PurgeExpiredRevokedTokensAsync(stoppingToken);
                        _lastRevokedTokenPurge = _time.GetUtcNow();
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Error purging expired revoked refresh tokens");
                    }
                }

                try
                {
                    await Task.Delay(TickInterval, _time, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// One tick: for every game, finalize each ended scoring day that is not finalized yet, oldest first
        /// (at most <see cref="MaxDaysPerTick"/> per game). See <see cref="ProcessGameAsync"/> for the window.
        /// Every game and every day gets its own DI scope (and DbContext), so one failure cannot poison the others.
        /// </summary>
        public async Task ProcessAllGamesAsync(CancellationToken ct = default)
        {
            List<(int Id, string Name, TimeSpan ResetTime)> games;
            using (var scope = _scopeFactory.CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
                games = (await repo.GetAllAsync()).Select(g => (g.Id, g.Name, g.ResetTime)).ToList();
            }

            foreach (var game in games)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    await ProcessGameAsync(game.Id, game.ResetTime, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error finalizing scoring days for game {GameId} ({GameName})", game.Id, game.Name);
                }
            }
        }

        /// <summary>Deletes revoked refresh tokens that have expired anyway (they can't be replayed any more).</summary>
        public async Task<int> PurgeExpiredRevokedTokensAsync(CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = _time.GetUtcNow().UtcDateTime;
            var purged = await db.RevokedRefreshTokens.Where(t => t.ExpiresAt < now).ExecuteDeleteAsync(ct);
            if (purged > 0)
                _logger.LogInformation("Purged {Count} expired revoked refresh tokens", purged);
            return purged;
        }

        /// <summary>
        /// Finalizes the game's unfinalized ended days, oldest first, at most <see cref="MaxDaysPerTick"/> per tick.
        /// The window is [max(firstSubmissionDay, min(lastFinalizedDay + 1, currentDay - GapScanDays)), currentDay - 1]:
        /// every gap in the last <see cref="GapScanDays"/> days is retried (e.g. a day that was un-finalized
        /// by an admin edit/delete whose re-finalization failed), and after an even longer outage the catch-up
        /// still starts right after the last finalized day. The finalizer is idempotent, so a raced day is harmless.
        /// </summary>
        private async Task ProcessGameAsync(int gameId, TimeSpan resetTime, CancellationToken ct)
        {
            // The current scoring day is still in progress; every day before it has ended.
            var currentDay = ScoringDayHelper.GetCurrentScoringDay(resetTime, _time.GetUtcNow().UtcDateTime).Date;
            var lastEndedDay = currentDay.AddDays(-1);

            DateTime? lastFinalized;
            DateTime? firstSubmissionDay;
            using (var scope = _scopeFactory.CreateScope())
            {
                lastFinalized = await scope.ServiceProvider.GetRequiredService<IScoringDayResultRepository>().GetLastFinalizedDayAsync(gameId);
                firstSubmissionDay = await scope.ServiceProvider.GetRequiredService<ISubmissionRepository>().GetFirstScoringDayAsync(gameId);
            }

            // A game without submissions has nothing to finalize.
            if (firstSubmissionDay == null) return;

            var start = currentDay.AddDays(-GapScanDays);
            var afterLastFinalized = lastFinalized.HasValue ? lastFinalized.Value.Date.AddDays(1) : firstSubmissionDay.Value.Date;
            if (afterLastFinalized < start) start = afterLastFinalized;
            if (firstSubmissionDay.Value.Date > start) start = firstSubmissionDay.Value.Date;
            if (start > lastEndedDay) return;

            HashSet<DateTime> finalized;
            using (var scope = _scopeFactory.CreateScope())
            {
                finalized = (await scope.ServiceProvider.GetRequiredService<IScoringDayResultRepository>()
                    .GetFinalizedDaysAsync(gameId, start, lastEndedDay)).ToHashSet();
            }

            var processed = 0;
            for (var day = start; day <= lastEndedDay && processed < MaxDaysPerTick; day = day.AddDays(1))
            {
                if (ct.IsCancellationRequested) break;
                if (finalized.Contains(day)) continue;
                processed++;

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var finalizer = scope.ServiceProvider.GetRequiredService<IScoringDayFinalizerService>();
                    await finalizer.FinalizeScoringDayAsync(gameId, day);
                }
                catch (Exception ex)
                {
                    // Stop at the first failure so days are finalized in order; the next tick retries this day.
                    _logger.LogError(ex, "Error finalizing scoring day {Day} for game {GameId}", day.ToString("yyyy-MM-dd"), gameId);
                    break;
                }
            }
        }
    }
}
