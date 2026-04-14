using DailyChallenges.Repositories;

namespace DailyChallenges.Services
{
    public class ScoringDayBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ScoringDayBackgroundService> _logger;
        private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(60);

        public ScoringDayBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ScoringDayBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
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

                await Task.Delay(TickInterval, stoppingToken);
            }
        }

        private async Task ProcessAllGamesAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var games = scope.ServiceProvider.GetRequiredService<IGameRepository>();
            var results = scope.ServiceProvider.GetRequiredService<IScoringDayResultRepository>();
            var finalizer = scope.ServiceProvider.GetRequiredService<IScoringDayFinalizerService>();

            var allGames = await games.GetAllAsync();

            foreach (var game in allGames)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    // The "current" scoring day is the day that is currently in progress.
                    // The "previous" scoring day is the one that just ended — that's what we want to finalize.
                    var currentDay = ScoringDayHelper.GetCurrentScoringDay(game.ResetTime);
                    var previousDay = currentDay.AddDays(-1);

                    // Skip if already processed
                    if (await results.ExistsAsync(game.Id, previousDay))
                        continue;

                    await finalizer.FinalizeScoringDayAsync(game.Id, previousDay);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error finalizing scoring day for game {GameId} ({GameName})", game.Id, game.Name);
                }
            }
        }
    }
}
