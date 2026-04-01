using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using System.Security.Claims;

namespace DailyChallenges.Services
{
    public class GameService : IGameService
    {
        private readonly IGameRepository _games;
        private readonly IFileStorage _files;
        private readonly ISubmissionRepository _subsRepo;
        private readonly ISubmissionService _submissionService;

        public GameService(IGameRepository games, IFileStorage files, ISubmissionRepository subsRepo, ISubmissionService submissionService)
        {
            _games = games;
            _files = files;
            _subsRepo = subsRepo;
            _submissionService = submissionService;
        }

        public async Task<List<GameDto>> GetAllAsync()
        {
            var games = await _games.GetAllAsync();
            return games.Select(g => DtoMapper.ToDto(g)).ToList();
        }

        public async Task<GameDto> CreateAsync(string name, IFormFile? image, string? resetTime, string? resetTimezoneId, string? url, string? description)
        {
            var game = new Game { Name = name };
            if (!string.IsNullOrWhiteSpace(resetTime))
            {
                if (TimeSpan.TryParse(resetTime, out var ts)) game.ResetTime = ts;
                else if (TimeSpan.TryParseExact(resetTime, "hh\\:mm", null, out var ts2)) game.ResetTime = ts2;
            }
            if (!string.IsNullOrWhiteSpace(resetTimezoneId)) game.ResetTimezoneId = resetTimezoneId;
            if (!string.IsNullOrWhiteSpace(url)) game.Url = url;
            if (!string.IsNullOrWhiteSpace(description)) game.Description = description;

            if (image != null && image.Length > 0)
            {
                var (data, contentType) = await _files.ReadFileAsync(image);
                game.ScreenshotData = data;
                game.ScreenshotContentType = contentType;
            }

            var created = await _games.CreateAsync(game);
            return DtoMapper.ToDto(created);
        }

        public async Task<Game?> GetByIdAsync(int id) => await _games.GetByIdAsync(id);

        public async Task<GameDto> UpdateAsync(int id, string? name, IFormFile? image, string? resetTime, string? resetTimezoneId, string? url, string? description)
        {
            var g = await _games.GetByIdAsync(id);
            if (g == null) throw new KeyNotFoundException("Game not found");
            if (!string.IsNullOrWhiteSpace(name)) g.Name = name;
            if (!string.IsNullOrWhiteSpace(url)) g.Url = url;
            if (!string.IsNullOrWhiteSpace(resetTime))
            {
                if (TimeSpan.TryParse(resetTime, out var ts)) g.ResetTime = ts;
                else if (TimeSpan.TryParseExact(resetTime, "hh\\:mm", null, out var ts2)) g.ResetTime = ts2;
            }
            if (!string.IsNullOrWhiteSpace(resetTimezoneId)) g.ResetTimezoneId = resetTimezoneId;
            if (description != null) g.Description = description;

            if (image != null && image.Length > 0)
            {
                var (data, contentType) = await _files.ReadFileAsync(image);
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
            // Determine whether the current user has already submitted today
            var currentDay = ScoringDayHelper.GetCurrentScoringDay(g.ResetTime, g.ResetTimezoneId);
            bool hasSubmittedToday = false;
            if (user != null)
            {
                hasSubmittedToday = await _submissionService.HasUserSubmittedForLatestAsync(id, user);
            }

            // Prefer numeric ScoreValue ordering (handled in the DB) to avoid heavy in-memory parsing/sorts.
            var numericCandidates = (await _subsRepo.GetTopByGameByScoreValueAsync(id, 2000)) ?? new List<Submission>();
            var usedNumeric = numericCandidates.Count > 0;

            List<Submission> subs = usedNumeric
                ? numericCandidates
                : (await _subsRepo.GetTopByGameAsync(id, 2000)) ?? new List<Submission>();

            if (user == null || !user.IsInRole("Admin"))
            {
                if (!hasSubmittedToday)
                    subs = subs.Where(s => ScoringDayHelper.GetScoringDay(s.CreatedAt, g.ResetTime, g.ResetTimezoneId) != currentDay).ToList();
            }

            List<SubmissionDto> topDtos;
            if (usedNumeric)
            {
                topDtos = subs.Take(50).Select(s => DtoMapper.ToDto(s)).ToList();
            }
            else
            {
                topDtos = subs
                    .Select(s => new { Sub = s, Num = ScoreParser.ParseScore(s.Score) })
                    .OrderByDescending(x => double.IsNaN(x.Num) ? double.NegativeInfinity : x.Num)
                    .ThenBy(x => x.Sub.CreatedAt)
                    .Take(50)
                    .Select(x => DtoMapper.ToDto(x.Sub))
                    .ToList();
            }

            AssignSequentialRanks(topDtos);
            return new HighscoreResult { Highscore = topDtos.FirstOrDefault(), Top = topDtos };
        }

        public async Task<HighscoreResult> GetPersonalHighscoreAsync(int id, int userId)
        {
            var g = await _games.GetByIdAsync(id);
            if (g == null) throw new KeyNotFoundException("Game not found");
            // Prefer numeric ScoreValue ordering when available
            var numericCandidates = (await _subsRepo.GetTopByGameByScoreValueAsync(id, 2000)) ?? new List<Submission>();
            var usedNumeric = numericCandidates.Count > 0;

            List<Submission> userSubs = usedNumeric
                ? numericCandidates.Where(s => s.UserId == userId).ToList()
                : ((await _subsRepo.GetTopByGameAsync(id, 2000)) ?? new List<Submission>()).Where(s => s.UserId == userId).ToList();

            List<SubmissionDto> topDtos;
            if (usedNumeric)
            {
                topDtos = userSubs.Take(50).Select(s => DtoMapper.ToDto(s)).ToList();
            }
            else
            {
                topDtos = userSubs
                    .Select(s => new { Sub = s, Num = ScoreParser.ParseScore(s.Score) })
                    .OrderByDescending(x => double.IsNaN(x.Num) ? double.NegativeInfinity : x.Num)
                    .ThenBy(x => x.Sub.CreatedAt)
                    .Take(50)
                    .Select(x => DtoMapper.ToDto(x.Sub))
                    .ToList();
            }

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

            return dto;
        }

        private void AssignSequentialRanks(List<SubmissionDto>? dtos)
        {
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
    }
}
