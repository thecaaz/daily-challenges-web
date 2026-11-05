using DailyChallenges.Exceptions;
using DailyChallenges.Achievements;
using DailyChallenges.Data;
using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services.Contracts;
using DailyChallenges.Services.Ranking;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace DailyChallenges.Services
{
    public class SubmissionService : ISubmissionService
    {
        private readonly TimeProvider _time;
        private readonly ISubmissionRepository _subs;
        private readonly IGameRepository _games;
        private readonly IFileStorage _files;
        private readonly IXpService _xp;
        private readonly AppDbContext _db;
        private readonly IUserSubmissionChecker _userSubmissionChecker;
        private readonly IAchievementService _achievements;
        private readonly ILogger<SubmissionService> _logger;
        private readonly IScoreVisibilityService _visibility;
        private readonly IScreenshotUrlSigner _signer;
        private readonly IScoringDayResultRepository _results;
        private readonly INotificationRepository _notifications;
        private readonly IServiceScopeFactory _scopeFactory;

        public SubmissionService(
            ISubmissionRepository subs,
            IGameRepository games,
            IFileStorage files,
            IXpService xp,
            AppDbContext db,
            IUserSubmissionChecker userSubmissionChecker,
            IAchievementService achievements,
            ILogger<SubmissionService> logger, TimeProvider time,
            IScoreVisibilityService visibility,
            IScreenshotUrlSigner signer,
            IScoringDayResultRepository results,
            INotificationRepository notifications,
            IServiceScopeFactory scopeFactory)
        {
            _results = results;
            _notifications = notifications;
            _scopeFactory = scopeFactory;
            _subs = subs;
            _games = games;
            _files = files;
            _xp = xp;
            _db = db;
            _userSubmissionChecker = userSubmissionChecker;
            _achievements = achievements;
            _logger = logger;
            _time = time;
            _visibility = visibility;
            _signer = signer;
        }

        public async Task<SubmissionPageDto> GetByGameAsync(int gameId, ClaimsPrincipal? user, DateTime? scoringDay = null, int page = 1, int pageSize = 50)
        {
            var game = await _games.GetByIdWithoutBlobAsync(gameId);
            if (game == null) return SubmissionDtoHelper.ToPageDto(new List<SubmissionDto>(), page, pageSize, false, 0);
            var strategy = RankingStrategyFactory.GetStrategy(game.RankingMode);

            var currentDay = _visibility.GetCurrentScoringDay(game);

            var userId = user.GetUserId();
            var hasSubmittedForLatest = await _userSubmissionChecker.HasUserSubmittedForDayAsync(userId, gameId, currentDay);

            // Without a day, the current scoring day is left out for non-submitters (as before).
            DateTime? excludeScoringDay = null;
            if (!scoringDay.HasValue && !hasSubmittedForLatest)
            {
                excludeScoringDay = currentDay;
            }

            // Asking for the current day explicitly: the rows are listed, but their scores are masked
            // unless the caller may see them (submitted or admin).
            var scoresHidden = scoringDay.HasValue
                && scoringDay.Value.Date == currentDay
                && !await _visibility.CanSeeDayAsync(user, game, currentDay);

            // Fetch a single page of submissions (repository will apply excludeScoringDay if provided)
            var (pageItems, totalCount) = await _subs.GetByGameFilteredAsync(gameId, page, pageSize, null, scoringDay, excludeScoringDay);

            if (scoresHidden)
            {
                var masked = pageItems
                    .Select(s => SubmissionDtoHelper.Mask(SubmissionDtoHelper.ToDtoWithScoringDay(s, s.ScoringDay.Date)))
                    .ToList();
                return SubmissionDtoHelper.ToPageDto(masked, page, pageSize, hasSubmittedForLatest, totalCount, scoresHidden: true);
            }

            // Compute winners only for scoring days present in the returned page
            var winnersByDay = await ComputeWinnersByDayAsync(gameId, pageItems, strategy);

            var mapped = pageItems.Select(s =>
            {
                var day = s.ScoringDay.Date;
                var isWinner = winnersByDay.TryGetValue(day, out var w) && w.Id == s.Id;
                var dto = SubmissionDtoHelper.ToDtoWithScoringDay(s, day, isWinner);
                // Current-day screenshots need a signed URL (the endpoint checks it); past days do not.
                return SubmissionDtoHelper.SignScreenshot(dto, _signer, day == currentDay);
            }).ToList();

            // If the caller requested a specific scoring day, populate 1-based ranks for scored submissions on that day.
            if (scoringDay.HasValue)
            {
                var scoredOrdered = await _subs.GetByGameAndDayByScoreValueAsync(gameId, scoringDay.Value.Date, strategy);
                var rankMap = new Dictionary<int, int>();
                int r = 1;
                foreach (var s in scoredOrdered)
                {
                    rankMap[s.Id] = r++;
                }

                foreach (var dto in mapped)
                {
                    if (rankMap.TryGetValue(dto.Id, out var rv)) dto.Rank = rv;
                    else dto.Rank = null;
                }
            }

            return SubmissionDtoHelper.ToPageDto(mapped, page, pageSize, hasSubmittedForLatest, totalCount);
        }

        public async Task<bool> HasUserSubmittedForLatestAsync(int gameId, ClaimsPrincipal? user)
        {
            return await _userSubmissionChecker.HasUserSubmittedForLatestAsync(gameId, user);
        }

        private DateTime GetCurrentScoringDay(Game? game)
        {
            return ScoringDayHelper.GetCurrentScoringDay(game?.ResetTime ?? TimeSpan.Zero, _time.GetUtcNow().UtcDateTime).Date;
        }

        private async Task<Dictionary<DateTime, Submission>> ComputeWinnersByDayAsync(int gameId, List<Submission> pageItems, IRankingStrategy strategy)
        {
            var daysInPage = pageItems.Select(s => s.ScoringDay.Date).Distinct().ToList();
            if (daysInPage.Count == 0) return new Dictionary<DateTime, Submission>();

            var winners = await _subs.GetWinnersForGameAndDaysAsync(gameId, daysInPage, strategy);
            return (winners ?? new List<Submission>()).ToDictionary(w => w.ScoringDay.Date);
        }

        public async Task<TodaySubmittersDto> GetTodaySubmittersAsync(int gameId)
        {
            var game = await _games.GetByIdWithoutBlobAsync(gameId);
            var currentDay = GetCurrentScoringDay(game);
            var usernames = await _subs.GetUsernamesForDayAsync(gameId, currentDay);
            return SubmissionDtoHelper.ToTodaySubmittersDto(usernames);
        }

        public async Task<List<SubmissionDto>> GetUnfilteredByGameAsync(int gameId)
        {
            var game = await _games.GetByIdWithoutBlobAsync(gameId);
            var all = await _subs.GetByGameAsync(gameId);
            var subs = all ?? new List<Submission>();
            var currentDay = GetCurrentScoringDay(game);

            var adminDtos = subs.Select(s =>
            {
                var dto = DtoMapper.ToDto(s);
                // The stored day, as everywhere else (a later reset-time change must not move old submissions).
                dto.ScoringDay = ScoringDayHelper.FormatScoringDay(s.ScoringDay);
                // Admin-only endpoint: admins may see the current day, so its screenshots get signed URLs.
                SubmissionDtoHelper.SignScreenshot(dto, _signer, s.ScoringDay.Date == currentDay);
                return dto;
            }).ToList();

            return adminDtos;
        }

        public async Task<List<string>> GetAvailableDatesAsync(int gameId)
        {
            var dates = await _subs.GetAvailableDatesAsync(gameId);
            return dates
                .Select(d => ScoringDayHelper.FormatScoringDay(d))
                .Distinct()
                .OrderByDescending(x => x)
                .ToList();
        }

        public async Task<SubmissionDto?> GetWinnerAsync(int gameId, DateTime? scoringDay, ClaimsPrincipal? user)
        {
            var game = await _games.GetByIdWithoutBlobAsync(gameId);
            if (game == null) return null;
            var strategy = RankingStrategyFactory.GetStrategy(game.RankingMode);

            // Without a day, the winner is the previous (completed) scoring day's.
            var currentDay = _visibility.GetCurrentScoringDay(game);
            var day = scoringDay?.Date ?? currentDay.AddDays(-1);

            // The current day's leader is hidden like its scores.
            if (!await _visibility.CanSeeDayAsync(user, game, day)) return null;

            var winner = await _subs.GetWinnerForGameAndDayAsync(gameId, day, strategy);
            if (winner == null) return null;

            var dto = SubmissionDtoHelper.ToDtoWithScoringDay(winner, winner.ScoringDay.Date, isDayWinner: true);
            dto.Rank = 1;
            return SubmissionDtoHelper.SignScreenshot(dto, _signer, winner.ScoringDay.Date == currentDay);
        }

        public async Task<(SubmissionDto Dto, int XpGain)> CreateAsync(int gameId, string score, string? username, IFormFile? screenshot, ClaimsPrincipal user)
        {
            var game = await _games.GetByIdWithoutBlobAsync(gameId);
            if (game == null) throw new ValidationException("invalid gameId");
            if (string.IsNullOrWhiteSpace(score)) throw new ValidationException("score is required");
            score = score.Trim();

            int? userId = ClaimsPrincipalExtensions.GetUserId(user);

            if (userId.HasValue)
            {
                // enforce one submission per user per game *per scoring day*.
                if (await _subs.ExistsForUserGameDayAsync(userId.Value, gameId, GetCurrentScoringDay(game)))
                    throw new ConflictException("User has already submitted for this game");
            }

            var submission = new Submission { GameId = gameId, Score = score, Username = username, UserId = userId, CreatedAt = _time.GetUtcNow().UtcDateTime };
            // Unparseable text is kept with a null ScoreValue: stored, but unranked.
            if (ScoreParser.TryParseDouble(score, out var parsedScore)) submission.ScoreValue = parsedScore;

            if (screenshot == null || screenshot.Length == 0)
            {
                throw new ArgumentException("screenshot is required");
            }

            var (data, contentType) = await _files.ReadFileAsync(screenshot);
            if (data == null || data.Length == 0) throw new ArgumentException("screenshot is required");
            submission.ScreenshotData = data;
            submission.ScreenshotContentType = contentType ?? string.Empty;

            if (userId.HasValue && !string.IsNullOrEmpty(user.Identity?.Name)) submission.Username = user.Identity.Name;
            // Compute and persist scoring day at write time so reads can query it directly.
            submission.ScoringDay = ScoringDayHelper.GetScoringDay(submission.CreatedAt, game.ResetTime);

            // Wrap submission creation and XP award in a single DB transaction so both
            // succeed or both roll back together. BEGIN IMMEDIATE (SQLite) serializes the user's XP
            // read-modify-write with other writers; the user row is first loaded inside it.
            Submission created;
            int xpGain = 0;
            await using (var tx = await _db.Database.BeginTransactionAsync())
            {
                try
                {
                    created = await _subs.CreateAsync(submission);

                    if (userId.HasValue)
                        xpGain = await _xp.AwardForSubmissionAsync(userId.Value, created.Id, created.ScoringDay);

                    await tx.CommitAsync();
                }
                catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.IsUniqueViolation())
                {
                    // Lost a race with a concurrent submission for the same user/game/day (unique index).
                    await tx.RollbackAsync();
                    _db.ChangeTracker.Clear();
                    throw new ConflictException("User has already submitted for this game");
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync();
                    _db.ChangeTracker.Clear();
                    _logger.LogError(ex, "Error creating submission for game {GameId} by user {UserId}", gameId, userId);
                    throw;
                }
            }

            // After the commit (outside the try above, so a committed transaction is never rolled back):
            // achievement failures are logged by the service and never undo the submission.
            if (userId.HasValue)
            {
                await _achievements.CheckAndAwardAsync(userId.Value, AchievementTrigger.Submission);
                await _achievements.CheckAndAwardAsync(userId.Value, AchievementTrigger.LevelUp);
            }

            // The creator may always see their own submission; it is on the current day, so sign the URL.
            var createdDto = SubmissionDtoHelper.ToDtoWithScoringDay(created, created.ScoringDay.Date);
            SubmissionDtoHelper.SignScreenshot(createdDto, _signer, true);
            return (createdDto, xpGain);
        }

        public async Task<SubmissionDto?> GetByIdAsync(int id, ClaimsPrincipal? user)
        {
            var s = await _subs.GetByIdWithoutBlobAsync(id);
            if (s == null) return null;

            var day = s.ScoringDay.Date;
            var dto = SubmissionDtoHelper.ToDtoWithScoringDay(s, day);

            var game = await _games.GetByIdWithoutBlobAsync(s.GameId);
            if (game == null) return SubmissionDtoHelper.Mask(dto);

            // The owner always sees their own submission; anyone else follows the hidden-scores rule.
            var userId = user.GetUserId();
            var isOwner = userId.HasValue && s.UserId == userId;
            if (!isOwner && !await _visibility.CanSeeDayAsync(user, game, day))
                return SubmissionDtoHelper.Mask(dto);

            if (s.ScoreValue.HasValue)
            {
                var strategy = RankingStrategyFactory.GetStrategy(game.RankingMode);
                var ordered = await _subs.GetByGameAndDayByScoreValueAsync(game.Id, day, strategy);
                var index = ordered.FindIndex(x => x.Id == s.Id);
                if (index >= 0)
                {
                    dto.Rank = index + 1;
                    dto.IsDayWinner = index == 0;
                }
            }

            return SubmissionDtoHelper.SignScreenshot(dto, _signer, _visibility.IsCurrentDay(game, day));
        }

        public async Task<(byte[]? Data, string? ContentType)> GetScreenshotAsync(int id, long? exp, string? sig)
        {
            var s = await _subs.GetByIdWithoutBlobAsync(id);
            if (s == null) return (null, null);

            // <img> requests carry no Bearer token, so a current-day screenshot is served only with a
            // valid signature (handed out with the DTO to callers allowed to see it). Past days are public.
            var game = await _games.GetByIdWithoutBlobAsync(s.GameId);
            if (game != null && _visibility.IsCurrentDay(game, s.ScoringDay) && !_signer.IsValid(id, exp, sig))
                throw new ForbiddenException("Screenshot is hidden until you submit for this day.");

            var (data, contentType) = await _subs.GetBlobAsync(id);
            if (data == null || data.Length == 0) return (null, null);
            return (data, contentType);
        }

        /// <summary>
        /// Admin score edit. On an already finalized day, a change of winner re-finalizes the day: the old
        /// winner's day_win XP and the day's result notifications are removed, then the finalizer runs again.
        /// </summary>
        public async Task<SubmissionDto> UpdateAsync(int id, string? score)
        {
            Submission updated;
            bool refinalize;
            await using (var tx = await _db.Database.BeginTransactionAsync())
            {
                try
                {
                    var s = await _subs.GetByIdAsync(id);
                    if (s == null) throw new KeyNotFoundException("Submission not found");
                    if (!string.IsNullOrWhiteSpace(score))
                    {
                        s.Score = score.Trim();
                        s.ScoreValue = ScoreParser.TryParseDouble(s.Score, out var parsedScore) ? parsedScore : null;
                    }
                    updated = await _subs.UpdateAsync(s);
                    refinalize = await UnfinalizeIfWinnerChangedAsync(updated.GameId, updated.ScoringDay);
                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    _db.ChangeTracker.Clear();
                    throw;
                }
            }

            if (refinalize) await RefinalizeAsync(updated.GameId, updated.ScoringDay);

            // Admin-only endpoint: admins may see the current day.
            var game = await _games.GetByIdWithoutBlobAsync(updated.GameId);
            var dto = SubmissionDtoHelper.ToDtoWithScoringDay(updated, updated.ScoringDay.Date);
            return SubmissionDtoHelper.SignScreenshot(dto, _signer, game != null && _visibility.IsCurrentDay(game, updated.ScoringDay));
        }

        /// <summary>
        /// Admin delete. In one transaction: revokes the submission's XP (negative "submission_revoked" event),
        /// deletes the row and, when the day is finalized and its winner changes, un-finalizes the day (see
        /// <see cref="UnfinalizeIfWinnerChangedAsync"/>). After the commit the finalizer recomputes the winner.
        /// The user may submit again for that day; the revoked event keeps their XP balance net-once.
        /// Deleting a missing submission is a no-op.
        /// </summary>
        public async Task DeleteAsync(int id)
        {
            int gameId;
            DateTime day;
            bool refinalize;
            await using (var tx = await _db.Database.BeginTransactionAsync())
            {
                try
                {
                    var s = await _subs.GetByIdWithoutBlobAsync(id);
                    if (s == null)
                    {
                        await tx.RollbackAsync();
                        return;
                    }
                    gameId = s.GameId;
                    day = s.ScoringDay.Date;

                    if (s.UserId.HasValue && s.XpAwarded is > 0)
                        await _xp.RevokeSubmissionAsync(s.UserId.Value, s.Id, s.GameId, day, s.XpAwarded.Value, "submission_deleted");

                    await _subs.DeleteAsync(id);
                    refinalize = await UnfinalizeIfWinnerChangedAsync(gameId, day);
                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    _db.ChangeTracker.Clear();
                    throw;
                }
            }

            _logger.LogInformation("Deleted submission {SubmissionId} (game {GameId}, day {Day}); re-finalizing: {Refinalize}",
                id, gameId, day.ToString("yyyy-MM-dd"), refinalize);

            if (refinalize) await RefinalizeAsync(gameId, day);
        }

        /// <summary>
        /// Runs inside the caller's transaction, after the change was saved. When the day is finalized and its
        /// recorded winner differs from the current leader, it revokes the old winner's day_win XP (deleting the
        /// event, so the unique index allows a new award), deletes the day's result notifications and the
        /// ScoringDayResult row. Returns true when the caller must re-run the finalizer after committing.
        /// </summary>
        private async Task<bool> UnfinalizeIfWinnerChangedAsync(int gameId, DateTime scoringDay)
        {
            var day = scoringDay.Date;
            var result = await _results.GetAsync(gameId, day);
            if (result == null) return false;

            var game = await _games.GetByIdWithoutBlobAsync(gameId);
            if (game == null) return false;
            var leader = await _subs.GetWinnerForGameAndDayAsync(gameId, day, RankingStrategyFactory.GetStrategy(game.RankingMode));
            if (leader?.UserId == result.WinnerUserId) return false;

            if (result.WinnerUserId.HasValue)
                await _xp.RevokeDayWinAsync(result.WinnerUserId.Value, gameId, day);
            await _notifications.DeleteDayResultNotificationsAsync(gameId, day);
            await _results.DeleteAsync(result);
            return true;
        }

        /// <summary>
        /// Re-runs the (idempotent) finalizer for the day on a fresh scope, as the background job does.
        /// Called after the admin change has committed, so a failure here must not fail the request: the
        /// change is applied, and the day is merely left un-finalized for now.
        /// </summary>
        private async Task RefinalizeAsync(int gameId, DateTime scoringDay)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IScoringDayFinalizerService>().FinalizeScoringDayAsync(gameId, scoringDay.Date);
            }
            catch (Exception ex)
            {
                // The background job rescans recent un-finalized days every tick and retries this one.
                _logger.LogWarning(ex, "Re-finalizing game {GameId} day {Day} failed; the background job will retry it",
                    gameId, scoringDay.ToString("yyyy-MM-dd"));
            }
        }
    }
}
