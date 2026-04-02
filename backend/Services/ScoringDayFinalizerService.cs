using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services.Ranking;
using Microsoft.Extensions.Options;

namespace DailyChallenges.Services
{
    public class ScoringDayFinalizerService : IScoringDayFinalizerService
    {
        private readonly IScoringDayResultRepository _results;
        private readonly ISubmissionRepository _subs;
        private readonly IGameRepository _games;
        private readonly INotificationRepository _notifications;
        private readonly IXpService _xp;
        private readonly XpConfig _cfg;
        private readonly ILogger<ScoringDayFinalizerService> _logger;

        public ScoringDayFinalizerService(
            IScoringDayResultRepository results,
            ISubmissionRepository subs,
            IGameRepository games,
            INotificationRepository notifications,
            IXpService xp,
            IOptions<XpConfig> cfg,
            ILogger<ScoringDayFinalizerService> logger)
        {
            _results = results;
            _subs = subs;
            _games = games;
            _notifications = notifications;
            _xp = xp;
            _cfg = cfg.Value;
            _logger = logger;
        }

        public async Task FinalizeScoringDayAsync(int gameId, DateTime scoringDay)
        {
            var day = scoringDay.Date;

            // Idempotency check
            if (await _results.ExistsAsync(gameId, day))
            {
                _logger.LogDebug("Scoring day {Day} for game {GameId} already finalized, skipping", day.ToString("yyyy-MM-dd"), gameId);
                return;
            }

            var game = await _games.GetByIdAsync(gameId);
            if (game == null)
            {
                _logger.LogWarning("Game {GameId} not found during finalization", gameId);
                return;
            }

            // Get all scored submissions for this game+day, ranked by the game's ranking strategy
            var strategy = RankingStrategyFactory.GetStrategy(game.RankingMode);
            var rankedSubmissions = await _subs.GetByGameAndDayByScoreValueAsync(gameId, day, strategy);

            if (rankedSubmissions.Count == 0)
            {
                // No scored submissions — record that we processed this day but with no winner
                await _results.CreateAsync(new ScoringDayResult
                {
                    GameId = gameId,
                    ScoringDay = day,
                    WinnerUserId = null,
                    ProcessedAt = DateTime.UtcNow
                });
                _logger.LogInformation("Finalized scoring day {Day} for game {GameName} (id={GameId}) — no scored submissions", day.ToString("yyyy-MM-dd"), game.Name, gameId);
                return;
            }

            var winner = rankedSubmissions[0];
            int? winnerUserId = winner.UserId;

            // Award win XP to the winner (if they're a registered user)
            if (winnerUserId.HasValue)
            {
                var xpAwarded = await _xp.AwardForDayWinAsync(winnerUserId.Value, gameId, day);
                _logger.LogInformation("Awarded {Xp} XP to user {UserId} for winning game {GameName} on {Day}", xpAwarded, winnerUserId.Value, game.Name, day.ToString("yyyy-MM-dd"));
            }

            // Build notifications for all registered participants
            var notifications = new List<Notification>();
            for (int i = 0; i < rankedSubmissions.Count; i++)
            {
                var sub = rankedSubmissions[i];
                if (!sub.UserId.HasValue) continue; // skip anonymous submissions

                int rank = i + 1;
                string message;
                string type;

                if (rank == 1)
                {
                    message = $"You won on {game.Name}! +{_cfg.WinXp} XP";
                    type = "day_win";
                }
                else
                {
                    message = $"You placed #{rank} on {game.Name}";
                    type = "day_placement";
                }

                notifications.Add(new Notification
                {
                    UserId = sub.UserId.Value,
                    GameId = gameId,
                    ScoringDay = day,
                    Message = message,
                    Type = type,
                    Rank = rank,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (notifications.Count > 0)
            {
                await _notifications.CreateBatchAsync(notifications);
            }

            // Record that this day has been finalized
            await _results.CreateAsync(new ScoringDayResult
            {
                GameId = gameId,
                ScoringDay = day,
                WinnerUserId = winnerUserId,
                ProcessedAt = DateTime.UtcNow
            });

            _logger.LogInformation("Finalized scoring day {Day} for game {GameName} (id={GameId}) — winner: {Winner}, {Count} notifications created",
                day.ToString("yyyy-MM-dd"), game.Name, gameId,
                winner.Username ?? winnerUserId?.ToString() ?? "anonymous",
                notifications.Count);
        }
    }
}
