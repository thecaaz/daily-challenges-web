using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

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

        public async Task<List<SubmissionDto>> GetByGameAsync(int gameId)
        {
            var subs = await _subs.GetByGameAsync(gameId);
            return subs.Select(s => DtoMapper.ToDto(s)).ToList();
        }

        public async Task<SubmissionDto> CreateAsync(int gameId, string score, string? username, IFormFile? screenshot, ClaimsPrincipal user)
        {
            var game = await _games.GetByIdAsync(gameId);
            if (game == null) throw new InvalidOperationException("invalid gameId");
            if (string.IsNullOrWhiteSpace(score)) throw new InvalidOperationException("score is required");

            int? userId = null;
            if (user.Identity?.IsAuthenticated ?? false)
            {
                var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(idClaim, out var parsed)) userId = parsed;
            }

            if (userId.HasValue)
            {
                var existing = await _subs.GetByGameAndUserAsync(gameId, userId.Value);
                if (existing != null) throw new InvalidOperationException("User has already submitted for this game");
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
