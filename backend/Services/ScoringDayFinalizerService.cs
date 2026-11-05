using DailyChallenges.Achievements;
using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services.Contracts;
using DailyChallenges.Services.Ranking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DailyChallenges.Services
{
    public class ScoringDayFinalizerService : IScoringDayFinalizerService
    {
        private readonly TimeProvider _time;
        private readonly AppDbContext _db;
        private readonly IScoringDayResultRepository _results;
        private readonly ISubmissionRepository _subs;
        private readonly IGameRepository _games;
        private readonly INotificationRepository _notifications;
        private readonly IXpService _xp;
        private readonly IAchievementService _achievements;
        private readonly XpConfig _cfg;
        private readonly ILogger<ScoringDayFinalizerService> _logger;

        public ScoringDayFinalizerService(
            AppDbContext db,
            IScoringDayResultRepository results,
            ISubmissionRepository subs,
            IGameRepository games,
            INotificationRepository notifications,
            IXpService xp,
            IAchievementService achievements,
            IOptions<XpConfig> cfg,
            ILogger<ScoringDayFinalizerService> logger, TimeProvider time)
        {
            _db = db;
            _results = results;
            _subs = subs;
            _games = games;
            _notifications = notifications;
            _xp = xp;
            _achievements = achievements;
            _cfg = cfg.Value;
            _logger = logger;
            _time = time;
        }

        /// <summary>
        /// Finalizes one game's scoring day in a single transaction: claim the day (insert the
        /// ScoringDayResult; a unique violation means it is already finalized), award the win XP, write the
        /// notifications, commit. Achievements (DayWin, LevelUp) are checked after the commit, so they see the
        /// committed result row and a failed check never undoes the finalization.
        /// Call it on a fresh DI scope: the user row must not be tracked before the transaction begins.
        /// </summary>
        public async Task FinalizeScoringDayAsync(int gameId, DateTime scoringDay)
        {
            var day = scoringDay.Date;

            // Cheap pre-check; the insert below is the real (race-proof) claim.
            if (await _results.ExistsAsync(gameId, day))
            {
                _logger.LogDebug("Scoring day {Day} for game {GameId} already finalized, skipping", day.ToString("yyyy-MM-dd"), gameId);
                return;
            }

            var game = await _games.GetByIdWithoutBlobAsync(gameId);
            if (game == null)
            {
                _logger.LogWarning("Game {GameId} not found during finalization", gameId);
                return;
            }

            int? winnerUserId = null;
            string? winnerName = null;
            int notificationCount = 0;

            // BEGIN IMMEDIATE on SQLite: XP read-modify-writes are serialized with submissions.
            await using (var tx = await _db.Database.BeginTransactionAsync())
            {
                try
                {
                    // All scored submissions for this game+day, ranked by the game's ranking strategy.
                    var strategy = RankingStrategyFactory.GetStrategy(game.RankingMode);
                    var rankedSubmissions = await _subs.GetByGameAndDayByScoreValueAsync(gameId, day, strategy);
                    var winner = rankedSubmissions.Count > 0 ? rankedSubmissions[0] : null;
                    winnerUserId = winner?.UserId;
                    winnerName = winner?.Username;

                    // 1. Claim the day. A unique violation means another run finalized it first.
                    try
                    {
                        await _results.CreateAsync(new ScoringDayResult
                        {
                            GameId = gameId,
                            ScoringDay = day,
                            WinnerUserId = winnerUserId,
                            ProcessedAt = _time.GetUtcNow().UtcDateTime
                        });
                    }
                    catch (DbUpdateException ex) when (ex.IsUniqueViolation())
                    {
                        await tx.RollbackAsync();
                        _db.ChangeTracker.Clear();
                        _logger.LogDebug("Scoring day {Day} for game {GameId} was finalized concurrently, skipping", day.ToString("yyyy-MM-dd"), gameId);
                        return;
                    }

                    // 2. Win XP (idempotent per user/game/day).
                    if (winnerUserId.HasValue)
                    {
                        var xpAwarded = await _xp.AwardForDayWinAsync(winnerUserId.Value, gameId, day);
                        _logger.LogInformation("Awarded {Xp} XP to user {UserId} for winning game {GameName} on {Day}", xpAwarded, winnerUserId.Value, game.Name, day.ToString("yyyy-MM-dd"));
                    }

                    // 3. Notifications for all registered participants.
                    var notifications = BuildNotifications(game, day, rankedSubmissions);
                    if (notifications.Count > 0)
                        await _notifications.CreateBatchAsync(notifications);
                    notificationCount = notifications.Count;

                    // 4. Commit.
                    await tx.CommitAsync();
                }
                catch
                {
                    // Nothing of this day is kept (no result row, no XP, no notifications); the next tick retries.
                    try { await tx.RollbackAsync(); } catch (Exception rollbackEx) { _logger.LogWarning(rollbackEx, "Rollback failed"); }
                    _db.ChangeTracker.Clear();
                    throw;
                }
            }

            _logger.LogInformation("Finalized scoring day {Day} for game {GameName} (id={GameId}) — winner: {Winner}, {Count} notifications created",
                day.ToString("yyyy-MM-dd"), game.Name, gameId,
                winnerName ?? winnerUserId?.ToString() ?? "none",
                notificationCount);

            // 5. After the commit: achievements see the committed ScoringDayResult and XP.
            if (winnerUserId.HasValue)
            {
                await _achievements.CheckAndAwardAsync(winnerUserId.Value, AchievementTrigger.DayWin);
                await _achievements.CheckAndAwardAsync(winnerUserId.Value, AchievementTrigger.LevelUp);
            }
        }

        private List<Notification> BuildNotifications(Game game, DateTime day, List<Submission> rankedSubmissions)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var notifications = new List<Notification>();
            for (int i = 0; i < rankedSubmissions.Count; i++)
            {
                var sub = rankedSubmissions[i];
                if (!sub.UserId.HasValue) continue; // skip anonymous submissions

                int rank = i + 1;
                var isWin = rank == 1;
                notifications.Add(new Notification
                {
                    UserId = sub.UserId.Value,
                    GameId = game.Id,
                    ScoringDay = day,
                    Message = isWin ? $"You won on {game.Name}! +{_cfg.WinXp} XP" : $"You placed #{rank} on {game.Name}",
                    Type = isWin ? "day_win" : "day_placement",
                    Rank = rank,
                    IsRead = false,
                    CreatedAt = now
                });
            }
            return notifications;
        }
    }
}
