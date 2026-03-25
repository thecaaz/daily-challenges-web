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

        public async Task<SubmissionPageDto> GetByGameAsync(int gameId, ClaimsPrincipal? user, int page = 1, int pageSize = 50)
        {
            var game = await _games.GetByIdAsync(gameId);
            var result = new SubmissionPageDto { Page = page, PageSize = pageSize };

            var currentDay = GetCurrentScoringDay(game);

            var userId = user.GetUserId();
            var hasSubmittedForLatest = await HasUserSubmittedForDayAsync(userId, gameId, currentDay, game);

            var (items, totalCount, availableDates) = await _subs.GetByGameFilteredAsync(gameId, page, pageSize, null, null);

            // If caller hasn't submitted today, hide current-day submissions
            var filtered = FilterSubmissionsForVisibility(items, hasSubmittedForLatest, game, currentDay);

            var mapped = MapAndAnnotate(filtered, game);
            var paged = mapped.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            result.Items = paged;
            result.HasSubmittedForLatest = hasSubmittedForLatest;
            result.TotalCount = filtered.Count;
            result.TotalPages = (int)Math.Ceiling(filtered.Count / (double)pageSize);
            result.HasMore = page < result.TotalPages;
            result.AvailableDates = availableDates
                .Select(d => d.ToString("yyyy-MM-dd"))
                .Distinct()
                .OrderByDescending(x => x)
                .ToList();

            return result;
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
            var exDay = ScoringDayHelper.GetScoringDay(latest.CreatedAt, game?.ResetTime ?? TimeSpan.Zero, game?.ResetTimezoneId ?? "UTC");
            return exDay == currentDay;
        }

        private List<Submission> FilterSubmissionsForVisibility(List<Submission> subs, bool hasSubmittedToday, Game? game, DateTime currentDay)
        {
            if (hasSubmittedToday) return subs;
            return subs.Where(s => ScoringDayHelper.GetScoringDay(s.CreatedAt, game?.ResetTime ?? TimeSpan.Zero, game?.ResetTimezoneId ?? "UTC") != currentDay).ToList();
        }

        private List<SubmissionDto> MapAndAnnotate(List<Submission> subs, Game? game)
        {
            return subs.Select(s =>
            {
                var dto = DtoMapper.ToDto(s);
                dto.ScoringDay = ScoringDayHelper.GetScoringDay(s.CreatedAt, game?.ResetTime ?? TimeSpan.Zero, game?.ResetTimezoneId ?? "UTC").ToString("yyyy-MM-dd");
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
            if (screenshot != null && screenshot.Length > 0)
            {
                var (data, contentType) = await _files.ReadFileAsync(screenshot);
                submission.ScreenshotData = data;
                submission.ScreenshotContentType = contentType;
            }

            if (userId.HasValue && !string.IsNullOrEmpty(user.Identity?.Name)) submission.Username = user.Identity.Name;
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
            if (!string.IsNullOrWhiteSpace(score)) s.Score = score;
            var updated = await _subs.UpdateAsync(s);
            return DtoMapper.ToDto(updated);
        }

        public async Task DeleteAsync(int id) => await _subs.DeleteAsync(id);
    }
}
