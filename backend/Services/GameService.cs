using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services.Ranking;
using System.Security.Claims;

namespace DailyChallenges.Services
{
    public class GameService : IGameService
    {
        private readonly IGameRepository _games;
        private readonly IFileStorage _files;
        private readonly ISubmissionRepository _subsRepo;
        private readonly ISubmissionService _submissionService;
        private readonly IFavoriteRepository _favRepo;

        public GameService(IGameRepository games, IFileStorage files, ISubmissionRepository subsRepo, ISubmissionService submissionService, IFavoriteRepository favRepo)
        {
            _games = games;
            _files = files;
            _subsRepo = subsRepo;
            _submissionService = submissionService;
            _favRepo = favRepo;
        }

        public async Task<List<GameDto>> GetAllAsync(ClaimsPrincipal? user = null)
        {
            var games = await _games.GetAllAsync();
            var dtos = games.Select(g => DtoMapper.ToDto(g)).ToList();

            var userId = user.GetUserId();
            if (!userId.HasValue) return dtos;

            var gameDays = games.Select(g => new { g.Id, Day = ScoringDayHelper.GetCurrentScoringDay(g.ResetTime).Date }).ToList();

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
            var game = new Game { Name = request.Name };
            if (!string.IsNullOrWhiteSpace(request.ResetTime))
            {
                if (TryParseResetTime(request.ResetTime, out var ts))
                {
                    if (request.ResetTimezoneOffsetMinutes.HasValue)
                    {
                        ts = NormalizeToUtc(ts, request.ResetTimezoneOffsetMinutes.Value);
                    }
                    game.ResetTime = ts;
                }
            }
            if (!string.IsNullOrWhiteSpace(request.Url)) game.Url = request.Url;
            if (!string.IsNullOrWhiteSpace(request.Description)) game.Description = request.Description;
            if (Enum.TryParse<RankingMode>(request.RankingMode, ignoreCase: true, out var mode)) game.RankingMode = mode;

            if (request.Image != null && request.Image.Length > 0)
            {
                var (data, contentType) = await _files.ReadFileAsync(request.Image);
                game.ScreenshotData = data;
                game.ScreenshotContentType = contentType;
            }

            var created = await _games.CreateAsync(game);
            return DtoMapper.ToDto(created);
        }

        public async Task<Game?> GetByIdAsync(int id) => await _games.GetByIdAsync(id);

        public async Task<GameDto> UpdateAsync(int id, GameUpdateRequest request)
        {
            var g = await _games.GetByIdAsync(id);
            if (g == null) throw new KeyNotFoundException("Game not found");
            if (!string.IsNullOrWhiteSpace(request.Name)) g.Name = request.Name;
            if (!string.IsNullOrWhiteSpace(request.Url)) g.Url = request.Url;
            if (!string.IsNullOrWhiteSpace(request.ResetTime))
            {
                if (TryParseResetTime(request.ResetTime, out var ts))
                {
                    if (request.ResetTimezoneOffsetMinutes.HasValue)
                    {
                        ts = NormalizeToUtc(ts, request.ResetTimezoneOffsetMinutes.Value);
                    }
                    g.ResetTime = ts;
                }
            }
            if (request.Description != null) g.Description = request.Description;
            if (!string.IsNullOrWhiteSpace(request.RankingMode) && Enum.TryParse<RankingMode>(request.RankingMode, ignoreCase: true, out var mode)) g.RankingMode = mode;

            if (request.Image != null && request.Image.Length > 0)
            {
                var (data, contentType) = await _files.ReadFileAsync(request.Image);
                g.ScreenshotData = data;
                g.ScreenshotContentType = contentType;
            }

            var updated = await _games.UpdateAsync(g);
            return DtoMapper.ToDto(updated);
        }

        public async Task DeleteAsync(int id) => await _games.DeleteAsync(id);

        public async Task<(byte[]? Data, string? ContentType)> GetImageAsync(int id)
        {
            var g = await _games.GetByIdAsync(id);
            if (g == null) return (null, null);
            if (g.ScreenshotData == null || g.ScreenshotData.Length == 0) return (null, null);
            return (g.ScreenshotData, g.ScreenshotContentType);
        }


        public async Task<HighscoreResult> GetHighscoreAsync(int id, ClaimsPrincipal? user)
        {
            var g = await _games.GetByIdAsync(id);
            if (g == null) throw new KeyNotFoundException("Game not found");
            var strategy = RankingStrategyFactory.GetStrategy(g.RankingMode);
            // Determine whether the current user has already submitted today
            var currentDay = ScoringDayHelper.GetCurrentScoringDay(g.ResetTime);
            bool hasSubmittedToday = false;
            if (user != null)
            {
                hasSubmittedToday = await _submissionService.HasUserSubmittedForLatestAsync(id, user);
            }

            // Prefer numeric ScoreValue ordering (handled in the DB) to avoid heavy in-memory parsing/sorts.
            var numericCandidates = (await _subsRepo.GetTopByGameByScoreValueAsync(id, 2000, strategy)) ?? new List<Submission>();
            var usedNumeric = numericCandidates.Count > 0;

            List<Submission> subs = usedNumeric
                ? numericCandidates
                : (await _subsRepo.GetTopByGameAsync(id, 2000)) ?? new List<Submission>();

            if (user == null || !user.IsInRole("Admin"))
            {
                    if (!hasSubmittedToday)
                    subs = subs.Where(s => ScoringDayHelper.GetScoringDay(s.CreatedAt, g.ResetTime) != currentDay).ToList();
            }

            List<SubmissionDto> topDtos;
            if (usedNumeric)
            {
                topDtos = subs.Take(50).Select(s => DtoMapper.ToDto(s)).ToList();
            }
            else
            {
                topDtos = SortSubmissionsInMemory(subs, g.RankingMode).Take(50).Select(s => DtoMapper.ToDto(s)).ToList();
            }

            AssignSequentialRanks(topDtos);
            return new HighscoreResult { Highscore = topDtos.FirstOrDefault(), Top = topDtos };
        }

        public async Task<HighscoreResult> GetPersonalHighscoreAsync(int id, int userId)
        {
            var g = await _games.GetByIdAsync(id);
            if (g == null) throw new KeyNotFoundException("Game not found");
            var strategy = RankingStrategyFactory.GetStrategy(g.RankingMode);
            var userSubs = (await _subsRepo.GetTopByGameByUserByScoreValueAsync(id, userId, 50, strategy)) ?? new List<Submission>();
            var topDtos = userSubs.Select(s => DtoMapper.ToDto(s)).ToList();

            AssignSequentialRanks(topDtos);
            return new HighscoreResult { Highscore = topDtos.FirstOrDefault(), Top = topDtos };
        }

        public async Task<GameOverviewDto> GetOverviewAsync(int gameId, ClaimsPrincipal? user, string? include = null, int top = 0)
        {
            var g = await _games.GetByIdAsync(gameId);
            if (g == null) throw new KeyNotFoundException("Game not found");

            var dto = new GameOverviewDto
            {
                Game = DtoMapper.ToDto(g)
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
            List<SubmissionDto>? topDtos = null;

            // Run sub-requests in parallel where possible, but handle failures individually
            var availTask = wantAvailable ? _submissionService.GetAvailableDatesAsync(gameId) : Task.FromResult(new List<string>());
            var hasTask = wantHasSubmitted ? _submissionService.HasUserSubmittedForLatestAsync(gameId, user) : Task.FromResult(false);
            Task<List<Submission>>? topTask = top > 0 ? _subsRepo.GetTopByGameAsync(gameId, top) : null;

            try
            {
                availableDates = await availTask;
            }
            catch (Exception ex)
            {
                errors.Add(new OverviewErrorDto { Part = "availableDates", Message = ex.Message });
                availableDates = new List<string>();
            }

            try
            {
                hasSubmitted = await hasTask;
            }
            catch (Exception ex)
            {
                errors.Add(new OverviewErrorDto { Part = "hasSubmitted", Message = ex.Message });
                hasSubmitted = false;
            }

            if (topTask != null)
            {
                try
                {
                    var topSubs = await topTask;
                    topDtos = topSubs.Select(s => DtoMapper.ToDto(s)).ToList();
                }
                catch (Exception ex)
                {
                    errors.Add(new OverviewErrorDto { Part = "top", Message = ex.Message });
                    topDtos = new List<SubmissionDto>();
                }
            }

            dto.AvailableDates = availableDates;
            dto.HasSubmittedForLatest = hasSubmitted;
            dto.Top = topDtos;
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

        private static IEnumerable<Submission> SortSubmissionsInMemory(List<Submission> subs, RankingMode mode)
        {
            var parsed = subs.Select(s => (Sub: s, Num: ScoreParser.ParseScore(s.Score)));
            return mode == RankingMode.Lowest
                ? parsed.OrderBy(x => double.IsNaN(x.Num) ? double.MaxValue : x.Num).ThenBy(x => x.Sub.CreatedAt).Select(x => x.Sub)
                : parsed.OrderByDescending(x => double.IsNaN(x.Num) ? double.NegativeInfinity : x.Num).ThenBy(x => x.Sub.CreatedAt).Select(x => x.Sub);
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

        private static bool TryParseResetTime(string input, out TimeSpan result)
        {
            if (TimeSpan.TryParse(input, out var ts))
            {
                result = ts;
                return true;
            }

            if (TimeSpan.TryParseExact(input, @"hh\:mm", null, out var ts2))
            {
                result = ts2;
                return true;
            }

            result = default;
            return false;
        }

        private static TimeSpan NormalizeToUtc(TimeSpan localTime, int offsetMinutes)
        {
            
            var utc = localTime + TimeSpan.FromMinutes(offsetMinutes);
            var dayTicks = TimeSpan.TicksPerDay;
            var ticks = ((utc.Ticks % dayTicks) + dayTicks) % dayTicks;
            return new TimeSpan(ticks);
        }
    }
}
