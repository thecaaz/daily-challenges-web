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
            // fetch a bounded set to avoid loading large collections; will page after filtering
            var all = await _subs.GetTopByGameAsync(gameId, 2000);
            var subs = all ?? new List<Submission>();

            var result = new SubmissionPageDto { Page = page, PageSize = pageSize };

            // Admins use the dedicated unfiltered admin endpoint; fall through
            // to the normal paged/hide-today logic so behavior is consistent.

            // Determine the current scoring day for this game
            var currentDay = ScoringDayHelper.GetCurrentScoringDay(
                game?.ResetTime ?? TimeSpan.Zero,
                game?.ResetTimezoneId ?? "UTC");

            // Resolve the calling user's id (null = unauthenticated)
            int? userId = ClaimsPrincipalExtensions.GetUserId(user);

            // Check whether this user has submitted for today's scoring day
            bool hasSubmittedToday = userId.HasValue && subs.Any(s =>
                s.UserId == userId.Value &&
                ScoringDayHelper.GetScoringDay(s.CreatedAt, game?.ResetTime ?? TimeSpan.Zero, game?.ResetTimezoneId ?? "UTC") == currentDay);

            // determine hasSubmittedToday for the caller (server-side)
            bool hasSubmittedTodayFlag = false;
            var parsedUserId = ClaimsPrincipalExtensions.GetUserId(user);
            if (parsedUserId.HasValue)
            {
                var latest = await _subs.GetByGameAndUserAsync(gameId, parsedUserId.Value);
                if (latest != null)
                {
                    var exDay = ScoringDayHelper.GetScoringDay(latest.CreatedAt, game?.ResetTime ?? TimeSpan.Zero, game?.ResetTimezoneId ?? "UTC");
                    var currentDay2 = ScoringDayHelper.GetCurrentScoringDay(game?.ResetTime ?? TimeSpan.Zero, game?.ResetTimezoneId ?? "UTC");
                    hasSubmittedTodayFlag = exDay == currentDay2;
                }
            }

            // If caller hasn't submitted today, hide current-day submissions
            List<Submission> filtered;
            if (!hasSubmittedTodayFlag)
            {
                filtered = subs.Where(s => ScoringDayHelper.GetScoringDay(s.CreatedAt, game?.ResetTime ?? TimeSpan.Zero, game?.ResetTimezoneId ?? "UTC") != currentDay).ToList();
            }
            else
            {
                filtered = subs;
            }

            var mapped = filtered.Select(s =>
            {
                var dto = DtoMapper.ToDto(s);
                dto.ScoringDay = ScoringDayHelper.GetScoringDay(s.CreatedAt, game?.ResetTime ?? TimeSpan.Zero, game?.ResetTimezoneId ?? "UTC").ToString("yyyy-MM-dd");
                return dto;
            }).ToList();
            var paged = mapped.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            result.Items = paged;
            result.HasSubmittedForLatest = hasSubmittedTodayFlag;
            result.TotalCount = filtered.Count;
            result.TotalPages = (int)Math.Ceiling(filtered.Count / (double)pageSize);
            result.HasMore = page < result.TotalPages;
            result.AvailableDates = mapped
                .Select(d => d.ScoringDay)
                .Where(x => x is not null)
                .Select(x => x!)
                .Distinct()
                .OrderByDescending(x => x)
                .ToList();
            return result;
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
