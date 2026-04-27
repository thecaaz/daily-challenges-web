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
        private readonly ISubmissionRepository _subs;
        private readonly IGameRepository _games;
        private readonly IFileStorage _files;
        private readonly IXpService _xp;
        private readonly AppDbContext _db;
        private readonly IUserSubmissionChecker _userSubmissionChecker;
        private readonly IAchievementService _achievements;
        private readonly ILogger<SubmissionService> _logger;

        public SubmissionService(
            ISubmissionRepository subs,
            IGameRepository games,
            IFileStorage files,
            IXpService xp,
            AppDbContext db,
            IUserSubmissionChecker userSubmissionChecker,
            IAchievementService achievements,
            ILogger<SubmissionService> logger)
        {
            _subs = subs;
            _games = games;
            _files = files;
            _xp = xp;
            _db = db;
            _userSubmissionChecker = userSubmissionChecker;
            _achievements = achievements;
            _logger = logger;
        }

        public async Task<SubmissionPageDto> GetByGameAsync(int gameId, ClaimsPrincipal? user, DateTime? scoringDay = null, int page = 1, int pageSize = 50)
        {
            var game = await _games.GetByIdAsync(gameId);
            var strategy = RankingStrategyFactory.GetStrategy(game?.RankingMode ?? RankingMode.Highest);

            var currentDay = GetCurrentScoringDay(game);

            var userId = user.GetUserId();
            var hasSubmittedForLatest = await _userSubmissionChecker.HasUserSubmittedForDayAsync(userId, gameId, currentDay, game);

            // Determine whether to exclude the current scoring day at the DB level
            DateTime? excludeScoringDay = null;
            if (!scoringDay.HasValue && !hasSubmittedForLatest)
            {
                excludeScoringDay = currentDay;
            }

            // Fetch a single page of submissions (repository will apply excludeScoringDay if provided)
            var (pageItems, totalCount, _) = await _subs.GetByGameFilteredAsync(gameId, page, pageSize, null, scoringDay, excludeScoringDay);

            // Compute winners only for scoring days present in the returned page using a single batched repository call
            var daysInPage = pageItems.Select(s => s.ScoringDay.Date).Distinct().ToList();
            var winnersByDay = new Dictionary<DateTime, Submission>();
            if (daysInPage.Count > 0)
            {
                var winners = await _subs.GetWinnersForGameAndDaysAsync(gameId, daysInPage, strategy);
                if (winners != null)
                {
                    foreach (var w in winners)
                    {
                        winnersByDay[w.ScoringDay.Date] = w;
                    }
                }
            }

            var mapped = pageItems.Select(s =>
            {
                var day = s.ScoringDay.Date;
                var isWinner = winnersByDay.TryGetValue(day, out var w) && w.Id == s.Id;
                return SubmissionDtoHelper.ToDtoWithScoringDay(s, day, isWinner);
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

        private async Task<List<Submission>> GetRecentSubmissionsAsync(int gameId)
        {
            var all = await _subs.GetTopByGameAsync(gameId, 2000);
            return all ?? new List<Submission>();
        }

        private DateTime GetCurrentScoringDay(Game? game)
        {
            return ScoringDayHelper.GetCurrentScoringDay(game?.ResetTime ?? TimeSpan.Zero);
        }

        public async Task<TodaySubmittersDto> GetTodaySubmittersAsync(int gameId)
        {
            var game = await _games.GetByIdAsync(gameId);
            var currentDay = GetCurrentScoringDay(game);
            var usernames = await _subs.GetUsernamesForDayAsync(gameId, currentDay);
            return SubmissionDtoHelper.ToTodaySubmittersDto(usernames);
        }


        private List<Submission> FilterSubmissionsForVisibility(List<Submission> subs, bool hasSubmittedToday, Game? game, DateTime currentDay)
        {
            if (hasSubmittedToday) return subs;
            return subs.Where(s => s.ScoringDay.Date != currentDay).ToList();
        }

        private List<SubmissionDto> MapAndAnnotate(List<Submission> subs, Game? game)
        {
            return subs.Select(s =>
            {
                var dto = DtoMapper.ToDto(s);
                dto.ScoringDay = ScoringDayHelper.FormatScoringDay(s.ScoringDay.Date);
                return dto;
            }).ToList();
        }

        public async Task<List<SubmissionDto>> GetUnfilteredByGameAsync(int gameId)
        {
            var game = await _games.GetByIdAsync(gameId);
            var all = await _subs.GetByGameAsync(gameId);
            var subs = all ?? new List<Submission>();

                var adminDtos = subs.Select(s =>
            {
                var dto = DtoMapper.ToDto(s);
                dto.ScoringDay = ScoringDayHelper.FormatScoringDay(ScoringDayHelper.GetScoringDay(s.CreatedAt, game?.ResetTime ?? TimeSpan.Zero));
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

        public async Task<SubmissionDto?> GetWinnerAsync(int gameId, DateTime? scoringDay = null)
        {
            var game = await _games.GetByIdAsync(gameId);
            if (game == null) return null;
            var strategy = RankingStrategyFactory.GetStrategy(game.RankingMode);

            DateTime day;
            if (scoringDay.HasValue) day = scoringDay.Value;
            else day = GetCurrentScoringDay(game);

            var winner = await _subs.GetWinnerForGameAndDayAsync(gameId, day, strategy);
            if (winner == null) return null;

            var dto = DtoMapper.ToDto(winner);
            dto.ScoringDay = ScoringDayHelper.FormatScoringDay(winner.ScoringDay.Date);
            dto.IsDayWinner = true;
            return dto;
        }

        public async Task<(SubmissionDto Dto, int XpGain)> CreateAsync(int gameId, string score, string? username, IFormFile? screenshot, ClaimsPrincipal user)
        {
            var game = await _games.GetByIdAsync(gameId);
            if (game == null) throw new InvalidOperationException("invalid gameId");
            if (string.IsNullOrWhiteSpace(score)) throw new InvalidOperationException("score is required");

            int? userId = ClaimsPrincipalExtensions.GetUserId(user);

            if (userId.HasValue)
            {
                // enforce one submission per user per game *per scoring day*.
                var latest = await _subs.GetByGameAndUserAsync(gameId, userId.Value);
                if (latest != null)
                {
                    DateTime newDay = ScoringDayHelper.GetCurrentScoringDay(game.ResetTime);
                    var exDay = latest.ScoringDay;
                    if (exDay == newDay) throw new InvalidOperationException("User has already submitted for this game");
                }
            }

            var submission = new Submission { GameId = gameId, Score = score, Username = username, UserId = userId };
            if (ScoreParser.TryParseInt(score, out var parsedScore)) submission.ScoreValue = parsedScore;

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
            // succeed or both roll back together.
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var created = await _subs.CreateAsync(submission);

                int xpGain = 0;
                if (userId.HasValue)
                    xpGain = await _xp.AwardForSubmissionAsync(userId.Value, created.Id, created.ScoringDay);

                await tx.CommitAsync();

                // Check achievements outside the transaction so a check failure never rolls back the submission.
                if (userId.HasValue)
                    await _achievements.CheckAndAwardAsync(userId.Value, AchievementTrigger.Submission);

                return (DtoMapper.ToDto(created), xpGain);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Error creating submission for game {GameId} by user {UserId}", gameId, userId);
                throw;
            }
        }

        public async Task<SubmissionDto?> GetByIdAsync(int id)
        {
            var s = await _subs.GetByIdAsync(id);
            if (s == null) return null;
            return DtoMapper.ToDto(s);
        }

        public async Task<(byte[]? Data, string? ContentType)> GetScreenshotAsync(int id)
        {
            var s = await _subs.GetByIdAsync(id);
            if (s == null) return (null, null);
            return (s.ScreenshotData, s.ScreenshotContentType);
        }

        public async Task<SubmissionDto> UpdateAsync(int id, string? score)
        {
            var s = await _subs.GetByIdAsync(id);
            if (s == null) throw new KeyNotFoundException("Submission not found");
            if (!string.IsNullOrWhiteSpace(score))
            {
                s.Score = score;
                if (ScoreParser.TryParseInt(score, out var parsedScore)) s.ScoreValue = parsedScore;
                else s.ScoreValue = null;
            }
            var updated = await _subs.UpdateAsync(s);
            return DtoMapper.ToDto(updated);
        }

        public async Task DeleteAsync(int id) => await _subs.DeleteAsync(id);
    }
}
