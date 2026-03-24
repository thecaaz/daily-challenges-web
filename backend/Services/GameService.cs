using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using Microsoft.AspNetCore.Http;
using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace DailyChallenges.Services
{
    public class GameService : IGameService
    {
        private readonly IGameRepository _games;
        private readonly IFileStorage _files;

        public GameService(IGameRepository games, IFileStorage files)
        {
            _games = games;
            _files = files;
        }

        public async Task<List<GameDto>> GetAllAsync()
        {
            var games = await _games.GetAllAsync();
            return games.Select(g => DtoMapper.ToDto(g)).ToList();
        }

        public async Task<GameDto> CreateAsync(string name, IFormFile? image, string? resetTime, string? resetTimezoneId, string? url)
        {
            var game = new Game { Name = name };
            if (!string.IsNullOrWhiteSpace(resetTime))
            {
                if (TimeSpan.TryParse(resetTime, out var ts)) game.ResetTime = ts;
                else if (TimeSpan.TryParseExact(resetTime, "hh\\:mm", null, out var ts2)) game.ResetTime = ts2;
            }
            if (!string.IsNullOrWhiteSpace(resetTimezoneId)) game.ResetTimezoneId = resetTimezoneId;
            if (!string.IsNullOrWhiteSpace(url)) game.Url = url;

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

        public async Task<GameDto> UpdateAsync(int id, string? name, IFormFile? image, string? resetTime, string? resetTimezoneId, string? url)
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

        private static double ParseScore(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return double.NaN;
            var m = Regex.Match(s, "-?\\d+(?:[.,]\\d+)?");
            if (!m.Success) return double.NaN;
            var raw = m.Value.Replace(',', '.');
            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
            return double.NaN;
        }

        public async Task<HighscoreResult> GetHighscoreAsync(int id, ClaimsPrincipal? user)
        {
            var g = await _games.GetByIdAsync(id);
            if (g == null) throw new KeyNotFoundException("Game not found");
            var subs = g.Submissions ?? new List<Submission>();

            // Filter current-day submissions for users who haven't submitted yet
            if (user == null || !user.IsInRole("Admin"))
            {
                int? userId = null;
                if (user?.Identity?.IsAuthenticated == true)
                {
                    var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (int.TryParse(idClaim, out var parsed)) userId = parsed;
                }

                var currentDay = ScoringDayHelper.GetCurrentScoringDay(g.ResetTime, g.ResetTimezoneId);
                bool hasSubmittedToday = userId.HasValue && subs.Any(s =>
                    s.UserId == userId.Value &&
                    ScoringDayHelper.GetScoringDay(s.CreatedAt, g.ResetTime, g.ResetTimezoneId) == currentDay);

                if (!hasSubmittedToday)
                    subs = subs.Where(s => ScoringDayHelper.GetScoringDay(s.CreatedAt, g.ResetTime, g.ResetTimezoneId) != currentDay).ToList();
            }

            var ordered = subs
                .Select(s => new { Sub = s, Num = ParseScore(s.Score) })
                .OrderByDescending(x => double.IsNaN(x.Num) ? double.NegativeInfinity : x.Num)
                .ThenBy(x => x.Sub.CreatedAt)
                .Take(50)
                .Select(x => DtoMapper.ToDto(x.Sub))
                .ToList();

            return new HighscoreResult { Highscore = ordered.FirstOrDefault(), Top = ordered };
        }

        public async Task<HighscoreResult> GetPersonalHighscoreAsync(int id, int userId)
        {
            var g = await _games.GetByIdAsync(id);
            if (g == null) throw new KeyNotFoundException("Game not found");
            var subs = g.Submissions?.Where(s => s.UserId == userId).ToList() ?? new List<Submission>();

            var ordered = subs
                .Select(s => new { Sub = s, Num = ParseScore(s.Score) })
                .OrderByDescending(x => double.IsNaN(x.Num) ? double.NegativeInfinity : x.Num)
                .ThenBy(x => x.Sub.CreatedAt)
                .Take(50)
                .Select(x => DtoMapper.ToDto(x.Sub))
                .ToList();

            return new HighscoreResult { Highscore = ordered.FirstOrDefault(), Top = ordered };
        }
    }
}
