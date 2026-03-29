using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System;
using System.Linq;

namespace DailyChallenges.Services
{
    public class SubmissionService : ISubmissionService
    {
        private readonly ISubmissionRepository _subs;
        private readonly IGameRepository _games;
        private readonly IFileStorage _files;

        public SubmissionService(ISubmissionRepository subs, IGameRepository games, IFileStorage files)
        {
            _subs = subs;
            _games = games;
            _files = files;
        }

        public async Task<SubmissionPageDto> GetByGameAsync(int gameId, ClaimsPrincipal? user, DateTime? scoringDay = null, int page = 1, int pageSize = 50)
        {
            var game = await _games.GetByIdAsync(gameId);
            var result = new SubmissionPageDto { Page = page, PageSize = pageSize };

            var currentDay = GetCurrentScoringDay(game);

            var userId = user.GetUserId();
            var hasSubmittedForLatest = await HasUserSubmittedForDayAsync(userId, gameId, currentDay, game);

            // Determine whether to exclude the current scoring day at the DB level
            DateTime? excludeScoringDay = null;
            if (!scoringDay.HasValue && !hasSubmittedForLatest)
            {
                excludeScoringDay = currentDay;
            }

            // Fetch a single page of submissions (repository will apply excludeScoringDay if provided)
            var (pageItems, totalCount, _) = await _subs.GetByGameFilteredAsync(gameId, page, pageSize, null, scoringDay, excludeScoringDay);

            // Compute winners only for scoring days present in the returned page by querying the repository per-day
            var daysInPage = pageItems.Select(s => s.ScoringDay.Date).Distinct().ToList();
            var winnersByDay = new Dictionary<DateTime, Submission>();
            foreach (var day in daysInPage)
            {
                var winner = await _subs.GetWinnerForGameAndDayAsync(gameId, day);
                if (winner != null) winnersByDay[day] = winner;
            }

            var mapped = pageItems.Select(s =>
            {
                var dto = DtoMapper.ToDto(s);
                dto.ScoringDay = s.ScoringDay.Date.ToString("yyyy-MM-dd");
                var day = s.ScoringDay.Date;
                dto.IsDayWinner = winnersByDay.TryGetValue(day, out var w) && w.Id == s.Id;
                return dto;
            }).ToList();

            result.Items = mapped;
            result.HasSubmittedForLatest = hasSubmittedForLatest;
            result.TotalCount = totalCount;
            result.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            result.HasMore = page < result.TotalPages;

            return result;
        }

        public async Task<bool> HasUserSubmittedForLatestAsync(int gameId, ClaimsPrincipal? user)
        {
            var game = await _games.GetByIdAsync(gameId);
            var currentDay = GetCurrentScoringDay(game);
            var userId = user.GetUserId();
            return await HasUserSubmittedForDayAsync(userId, gameId, currentDay, game);
        }

        private async Task<List<Submission>> GetRecentSubmissionsAsync(int gameId)
        {
            var all = await _subs.GetTopByGameAsync(gameId, 2000);
            return all ?? new List<Submission>();
        }

        private DateTime GetCurrentScoringDay(Game? game)
        {
            return ScoringDayHelper.GetCurrentScoringDay(game?.ResetTime ?? TimeSpan.Zero, game?.ResetTimezoneId ?? "UTC");
        }

        private async Task<bool> HasUserSubmittedForDayAsync(int? userId, int gameId, DateTime currentDay, Game? game)
        {
            if (!userId.HasValue) return false;
            var latest = await _subs.GetByGameAndUserAsync(gameId, userId.Value);
            if (latest == null) return false;
            return latest.ScoringDay.Date == currentDay;
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
                dto.ScoringDay = s.ScoringDay.Date.ToString("yyyy-MM-dd");
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
                dto.ScoringDay = ScoringDayHelper.GetScoringDay(s.CreatedAt, game?.ResetTime ?? TimeSpan.Zero, game?.ResetTimezoneId ?? "UTC").ToString("yyyy-MM-dd");
                return dto;
            }).ToList();

            return adminDtos;
        }

        public async Task<List<string>> GetAvailableDatesAsync(int gameId)
        {
            var dates = await _subs.GetAvailableDatesAsync(gameId);
            return dates
                .Select(d => d.ToString("yyyy-MM-dd"))
                .Distinct()
                .OrderByDescending(x => x)
                .ToList();
        }

        public async Task<SubmissionDto> CreateAsync(int gameId, string score, string? username, IFormFile? screenshot, ClaimsPrincipal user)
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
                    DateTime newDay = ScoringDayHelper.GetCurrentScoringDay(game.ResetTime, game.ResetTimezoneId);
                    var exDay = ScoringDayHelper.GetScoringDay(latest.CreatedAt, game.ResetTime, game.ResetTimezoneId);
                    if (exDay == newDay) throw new InvalidOperationException("User has already submitted for this game");
                }
            }

            var submission = new Submission { GameId = gameId, Score = score, Username = username, UserId = userId };
            if (int.TryParse(score, out var parsedScore)) submission.ScoreValue = parsedScore;
            if (screenshot != null && screenshot.Length > 0)
            {
                var (data, contentType) = await _files.ReadFileAsync(screenshot);
                submission.ScreenshotData = data;
                submission.ScreenshotContentType = contentType;
            }

            if (userId.HasValue && !string.IsNullOrEmpty(user.Identity?.Name)) submission.Username = user.Identity.Name;
            // Compute and persist scoring day at write time so reads can query it directly.
            submission.ScoringDay = ScoringDayHelper.GetScoringDay(submission.CreatedAt, game.ResetTime, game.ResetTimezoneId);

            var created = await _subs.CreateAsync(submission);
            return DtoMapper.ToDto(created);
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
                if (int.TryParse(score, out var parsedScore)) s.ScoreValue = parsedScore;
                else s.ScoreValue = null;
            }
            var updated = await _subs.UpdateAsync(s);
            return DtoMapper.ToDto(updated);
        }

        public async Task DeleteAsync(int id) => await _subs.DeleteAsync(id);
    }
}
