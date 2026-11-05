using DailyChallenges.Data;
using DailyChallenges.DTOs;
using DailyChallenges.Exceptions;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services.Ranking;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;

namespace DailyChallenges.Services
{
    public class GameService : IGameService
    {
        private readonly TimeProvider _time;
        private readonly IGameRepository _games;
        private readonly IFileStorage _files;
        private readonly ISubmissionRepository _subsRepo;
        private readonly ISubmissionService _submissionService;
        private readonly IFavoriteRepository _favRepo;
        private readonly IScoreVisibilityService _visibility;
        private readonly IScreenshotUrlSigner _signer;
        private readonly AppDbContext _db;
        private readonly IScoringDayResultRepository _results;

        public GameService(IGameRepository games, IFileStorage files, ISubmissionRepository subsRepo, ISubmissionService submissionService, IFavoriteRepository favRepo, TimeProvider time,
            IScoreVisibilityService visibility, IScreenshotUrlSigner signer, AppDbContext db, IScoringDayResultRepository results)
        {
            _db = db;
            _results = results;
            _visibility = visibility;
            _signer = signer;
            _games = games;
            _files = files;
            _subsRepo = subsRepo;
            _submissionService = submissionService;
            _favRepo = favRepo;
            _time = time;
        }

        public async Task<List<GameDto>> GetAllAsync(ClaimsPrincipal? user = null)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var games = await _games.GetAllAsync();
            var dtos = games.Select(g => DtoMapper.ToDto(g, nowUtc: now)).ToList();

            var userId = user.GetUserId();
            if (!userId.HasValue) return dtos;

            var gameDays = games.Select(g => new { g.Id, Day = ScoringDayHelper.GetCurrentScoringDay(g.ResetTime, now).Date }).ToList();

            var distinctDays = gameDays.Select(x => x.Day).Distinct().ToList();
            var gameIds = gameDays.Select(x => x.Id).ToList();

            var userSubs = await _subsRepo.GetByUserAndGamesAndDaysAsync(userId.Value, gameIds, distinctDays);

            var submittedSet = new HashSet<(int, DateTime)>(userSubs.Select(s => (s.GameId, s.ScoringDay.Date)));

            var favoriteIds = await _favRepo.GetFavoriteGameIdsForUserAsync(userId.Value);
            var favSet = new HashSet<int>(favoriteIds);

            foreach (var dto in dtos)
            {
                var day = gameDays.First(x => x.Id == dto.Id).Day;
                dto.HasSubmittedForLatest = submittedSet.Contains((dto.Id, day));
                dto.IsFavorite = favSet.Contains(dto.Id);
            }

            return dtos;
        }

        public async Task<GameDto> CreateAsync(GameCreateRequest request)
        {
            // Validate everything before writing anything.
            var resetTime = ParseResetTime(request.ResetTime);
            var url = ParseUrl(request.Url);
            var rankingMode = ParseRankingMode(request.RankingMode);

            var game = new Game { Name = request.Name };
            if (resetTime.HasValue) game.ResetTime = resetTime.Value;
            if (!string.IsNullOrEmpty(url)) game.Url = url;
            if (!string.IsNullOrWhiteSpace(request.Description)) game.Description = request.Description;
            if (rankingMode.HasValue) game.RankingMode = rankingMode.Value;

            if (request.Image != null && request.Image.Length > 0)
            {
                var (data, contentType) = await _files.ReadFileAsync(request.Image);
                game.ScreenshotData = data;
                game.ScreenshotContentType = contentType;
            }

            var created = await _games.CreateAsync(game);
            return DtoMapper.ToDto(created, nowUtc: _time.GetUtcNow().UtcDateTime);
        }

        public async Task<Game?> GetByIdAsync(int id) => await _games.GetByIdAsync(id);

        /// <summary>
        /// Updates a game. Omitted (null) fields stay unchanged; "" clears Url and Description.
        /// A ResetTime change re-derives the stored ScoringDay of the game's not-yet-finalized submissions
        /// (and of their XpEvents) in the same transaction, or fails with a ConflictException.
        /// </summary>
        public async Task<GameDto> UpdateAsync(int id, GameUpdateRequest request)
        {
            // Validate everything before writing anything.
            var resetTime = ParseResetTime(request.ResetTime);
            var url = ParseUrl(request.Url);
            var rankingMode = ParseRankingMode(request.RankingMode);

            byte[]? imageData = null;
            string? imageContentType = null;
            if (request.Image != null && request.Image.Length > 0)
                (imageData, imageContentType) = await _files.ReadFileAsync(request.Image);

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var g = await _games.GetByIdAsync(id);
                if (g == null) throw new KeyNotFoundException("Game not found");

                if (!string.IsNullOrWhiteSpace(request.Name)) g.Name = request.Name;
                if (url != null) g.Url = url.Length == 0 ? null : url;
                if (request.Description != null) g.Description = request.Description.Length == 0 ? null : request.Description;
                if (rankingMode.HasValue) g.RankingMode = rankingMode.Value;
                if (imageData != null && imageData.Length > 0)
                {
                    g.ScreenshotData = imageData;
                    g.ScreenshotContentType = imageContentType;
                }

                if (resetTime.HasValue && resetTime.Value != g.ResetTime)
                {
                    await RederiveScoringDaysAsync(g.Id, resetTime.Value);
                    g.ResetTime = resetTime.Value;
                }

                var updated = await _games.UpdateAsync(g);
                await tx.CommitAsync();
                return DtoMapper.ToDto(updated, nowUtc: _time.GetUtcNow().UtcDateTime);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                await tx.RollbackAsync();
                _db.ChangeTracker.Clear();
                throw new ConflictException("Changing the reset time would merge two submissions of the same user into one scoring day.");
            }
            catch
            {
                await tx.RollbackAsync();
                _db.ChangeTracker.Clear();
                throw;
            }
        }

        /// <summary>
        /// Recomputes Submission.ScoringDay (and the matching XpEvents.ScoringDay) of every submission of the
        /// game after its last finalized day, for the new reset time. Finalized days are never touched, so
        /// results, win XP and notifications stay consistent. Runs inside the caller's transaction.
        /// </summary>
        private async Task RederiveScoringDaysAsync(int gameId, TimeSpan newResetTime)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var lastFinalized = await _results.GetLastFinalizedDayAsync(gameId);

            // The new current day must not be a finalized day either, or new submissions would land in it.
            var newCurrentDay = ScoringDayHelper.GetCurrentScoringDay(newResetTime, now).Date;
            if (lastFinalized.HasValue && newCurrentDay <= lastFinalized.Value)
                throw new ConflictException($"With this reset time the current scoring day would be {ScoringDayHelper.FormatScoringDay(newCurrentDay)}, which is already finalized. Try again after the new reset time has passed.");

            var query = _db.Submissions.Where(s => s.GameId == gameId);
            if (lastFinalized.HasValue)
            {
                var after = lastFinalized.Value;
                query = query.Where(s => s.ScoringDay > after);
            }
            var rows = await query
                .Select(s => new { s.Id, s.UserId, s.CreatedAt, s.ScoringDay })
                .ToListAsync();

            var moves = rows
                .Select(r => new { r.Id, r.UserId, OldDay = r.ScoringDay.Date, NewDay = ScoringDayHelper.GetScoringDay(r.CreatedAt, newResetTime).Date })
                .ToList();

            if (lastFinalized.HasValue && moves.Any(m => m.NewDay <= lastFinalized.Value))
                throw new ConflictException("Changing the reset time would move submissions into an already finalized scoring day.");

            if (moves.Where(m => m.UserId.HasValue).GroupBy(m => (m.UserId, m.NewDay)).Any(grp => grp.Count() > 1))
                throw new ConflictException("Changing the reset time would merge two submissions of the same user into one scoring day.");

            var changed = moves.Where(m => m.NewDay != m.OldDay).ToList();
            if (changed.Count == 0) return;

            // Two passes so the (UserId, GameId, ScoringDay) unique index never sees a transient duplicate
            // (SQLite checks it row by row): park each moved row on its own placeholder day, then set the real day.
            var placeholderBase = new DateTime(1000, 1, 1);
            for (int i = 0; i < changed.Count; i++)
            {
                var subId = changed[i].Id;
                var placeholder = placeholderBase.AddDays(i);
                await _db.Submissions.Where(s => s.Id == subId)
                    .ExecuteUpdateAsync(set => set.SetProperty(s => s.ScoringDay, placeholder));
            }
            foreach (var m in changed)
            {
                var subId = m.Id;
                var newDay = m.NewDay;
                await _db.Submissions.Where(s => s.Id == subId)
                    .ExecuteUpdateAsync(set => set.SetProperty(s => s.ScoringDay, newDay));
                DateTime? newEventDay = newDay;
                await _db.XpEvents.Where(e => e.SubmissionId == subId)
                    .ExecuteUpdateAsync(set => set.SetProperty(e => e.ScoringDay, newEventDay));
            }
        }

        public async Task DeleteAsync(int id) => await _games.DeleteAsync(id);

        public async Task<(byte[]? Data, string? ContentType)> GetImageAsync(int id)
        {
            var (data, contentType) = await _games.GetBlobAsync(id);
            if (data == null || data.Length == 0) return (null, null);
            return (data, contentType);
        }


        public async Task<HighscoreResult> GetHighscoreAsync(int id, ClaimsPrincipal? user)
        {
            var g = await _games.GetByIdWithoutBlobAsync(id);
            if (g == null) throw new KeyNotFoundException("Game not found");
            var strategy = RankingStrategyFactory.GetStrategy(g.RankingMode);

            // Hidden-scores rule: the current scoring day is left out (in SQL, on the stored ScoringDay)
            // unless the caller may see it. Ordering and the top-50 limit also run in SQL.
            var currentDay = _visibility.GetCurrentScoringDay(g);
            var canSeeCurrent = await _visibility.CanSeeDayAsync(user, g, currentDay);
            var subs = await _subsRepo.GetTopByGameByScoreValueAsync(id, 50, strategy, canSeeCurrent ? null : currentDay);

            var topDtos = subs
                .Select(s => SubmissionDtoHelper.SignScreenshot(
                    SubmissionDtoHelper.ToDtoWithScoringDay(s, s.ScoringDay.Date), _signer, s.ScoringDay.Date == currentDay))
                .ToList();

            AssignSequentialRanks(topDtos);
            return new HighscoreResult { Highscore = topDtos.FirstOrDefault(), Top = topDtos };
        }

        public async Task<HighscoreResult> GetPersonalHighscoreAsync(int id, int userId)
        {
            var g = await _games.GetByIdWithoutBlobAsync(id);
            if (g == null) throw new KeyNotFoundException("Game not found");
            var strategy = RankingStrategyFactory.GetStrategy(g.RankingMode);
            var currentDay = _visibility.GetCurrentScoringDay(g);
            var userSubs = (await _subsRepo.GetTopByGameByUserByScoreValueAsync(id, userId, 50, strategy)) ?? new List<Submission>();
            // The caller's own submissions: always visible, current-day screenshots signed.
            var topDtos = userSubs
                .Select(s => SubmissionDtoHelper.SignScreenshot(
                    SubmissionDtoHelper.ToDtoWithScoringDay(s, s.ScoringDay.Date), _signer, s.ScoringDay.Date == currentDay))
                .ToList();

            AssignSequentialRanks(topDtos);
            return new HighscoreResult { Highscore = topDtos.FirstOrDefault(), Top = topDtos };
        }

        public async Task<GameOverviewDto> GetOverviewAsync(int gameId, ClaimsPrincipal? user, string? include = null)
        {
            var g = await _games.GetByIdWithoutBlobAsync(gameId);
            if (g == null) throw new KeyNotFoundException("Game not found");

            var dto = new GameOverviewDto
            {
                Game = DtoMapper.ToDto(g, nowUtc: _time.GetUtcNow().UtcDateTime)
            };

            var errors = new List<OverviewErrorDto>();

            var includeAll = string.IsNullOrEmpty(include);
            HashSet<string>? includeSet = null;
            if (!includeAll)
            {
                includeSet = new HashSet<string>(
                    include!
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                );
            }

            var wantAvailable = includeAll || (includeSet != null && includeSet.Contains("availableDates"));
            var wantHasSubmitted = includeAll || (includeSet != null && includeSet.Contains("hasSubmitted"));
            List<string>? availableDates = null;
            bool hasSubmitted = false;

            // Sub-requests run one after another (they share the scoped DbContext, which allows only one
            // operation at a time), and failures are reported per part.
            try
            {
                availableDates = wantAvailable ? await _submissionService.GetAvailableDatesAsync(gameId) : new List<string>();
            }
            catch (Exception ex)
            {
                errors.Add(new OverviewErrorDto { Part = "availableDates", Message = ex.Message });
                availableDates = new List<string>();
            }

            try
            {
                hasSubmitted = wantHasSubmitted && await _submissionService.HasUserSubmittedForLatestAsync(gameId, user);
            }
            catch (Exception ex)
            {
                errors.Add(new OverviewErrorDto { Part = "hasSubmitted", Message = ex.Message });
                hasSubmitted = false;
            }

            dto.AvailableDates = availableDates;
            dto.HasSubmittedForLatest = hasSubmitted;
            dto.Errors = errors.Count > 0 ? errors : null;

            // mark favorite flag for the requesting user when available
            var requestingUserId = user.GetUserId();
            if (requestingUserId.HasValue && dto.Game != null)
            {
                var favIds = await _favRepo.GetFavoriteGameIdsForUserAsync(requestingUserId.Value);
                dto.Game.IsFavorite = favIds.Contains(dto.Game.Id);
            }

            return dto;
        }

        private void AssignSequentialRanks(List<SubmissionDto>? dtos)        {
            if (dtos == null) return;
            int rank = 1;
            foreach (var dto in dtos)
            {
                if (dto.ScoreValue.HasValue)
                {
                    dto.Rank = rank++;
                }
                else
                {
                    dto.Rank = null;
                }
            }
        }

        /// <summary>Null/blank → null (unchanged). Otherwise UTC "HH:mm", 00:00–23:59, or a ValidationException.</summary>
        private static TimeSpan? ParseResetTime(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            if (TimeSpan.TryParseExact(input.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var ts)
                && ts >= TimeSpan.Zero && ts < TimeSpan.FromDays(1))
                return ts;
            throw new ValidationException("resetTime must be a UTC time \"HH:mm\" between 00:00 and 23:59.");
        }

        /// <summary>
        /// Null → null (unchanged). "" (or blank) → "" (clear). Otherwise an absolute http(s) URL,
        /// or a ValidationException.
        /// </summary>
        private static string? ParseUrl(string? input)
        {
            if (input == null) return null;
            var trimmed = input.Trim();
            if (trimmed.Length == 0) return string.Empty;
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && !string.IsNullOrEmpty(uri.Host))
                return trimmed;
            throw new ValidationException("url must be an absolute http:// or https:// URL.");
        }

        /// <summary>Null/blank → null (unchanged). Otherwise a RankingMode name (case-insensitive), or a ValidationException.</summary>
        private static RankingMode? ParseRankingMode(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var name = Enum.GetNames<RankingMode>().FirstOrDefault(n => string.Equals(n, input.Trim(), StringComparison.OrdinalIgnoreCase));
            if (name == null)
                throw new ValidationException($"rankingMode must be one of: {string.Join(", ", Enum.GetNames<RankingMode>().Select(n => n.ToLowerInvariant()))}.");
            return Enum.Parse<RankingMode>(name);
        }
    }
}
