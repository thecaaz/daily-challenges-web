using System.Security.Claims;
using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace DailyChallenges.Services
{
    public class UserProfileService : Contracts.IUserProfileService
    {
        private readonly TimeProvider _time;
        private readonly IUserProfileRepository _repo;
        private readonly ISubmissionRepository _submissionRepo;
        private readonly IGameRepository _games;
        private readonly LevelCalculator _levelCalc;
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(60);

        public UserProfileService(IUserProfileRepository repo, ISubmissionRepository submissionRepo, IGameRepository games, LevelCalculator levelCalc, IMemoryCache cache, TimeProvider time)
        {
            _repo = repo;
            _submissionRepo = submissionRepo;
            _games = games;
            _levelCalc = levelCalc;
            _cache = cache;
            _time = time;
        }

        public async Task<UserProfileDto?> GetProfileAsync(int userId, int topGames = 10, ClaimsPrincipal? viewer = null)
        {
            // Hidden-scores rule, simplified so the cache still works: each game's current scoring day is
            // left out unless the viewer is the profile owner or an admin (stricter than the full rule).
            var canSeeCurrent = viewer.GetUserId() == userId || viewer.IsAdmin();

            var key = $"UserProfile:{userId}:{topGames}:{(canSeeCurrent ? "all" : "past")}";
            if (_cache.TryGetValue<UserProfileDto?>(key, out var cached)) return cached;

            var user = await _repo.GetByIdAsync(userId);
            if (user == null) return null;

            var now = _time.GetUtcNow().UtcDateTime;
            var games = await _games.GetAllAsync();
            var currentDayByGame = games.ToDictionary(g => g.Id, g => ScoringDayHelper.GetCurrentScoringDay(g.ResetTime, now).Date);

            var hiddenIds = canSeeCurrent ? new HashSet<int>() : await GetCurrentDaySubmissionIdsAsync(userId, currentDayByGame);

            var top = await _repo.GetMostPlayedGamesAsync(userId, topGames, hiddenIds);

            if (top.Count > 0)
            {
                var gameIds = top.Select(g => g.GameId).ToList();

                // Sparkline: the last 30 scoring days of each game, ending at that game's current scoring day
                // (a late reset can make "today" tomorrow's UTC date). Query the union, filter per game.
                const int window = 30;
                var gameDays = gameIds.Where(currentDayByGame.ContainsKey).Select(id => currentDayByGame[id]).ToList();
                var maxDay = gameDays.Count > 0 ? gameDays.Max() : now.Date;
                var minDay = (gameDays.Count > 0 ? gameDays.Min() : now.Date).AddDays(-(window - 1));
                var unionDays = Enumerable.Range(0, (maxDay - minDay).Days + 1).Select(i => minDay.AddDays(i)).ToList();

                var history = (await _submissionRepo.GetByUserAndGamesAndDaysAsync(userId, gameIds, unionDays))
                    .Where(s => !hiddenIds.Contains(s.Id))
                    .Where(s => currentDayByGame.TryGetValue(s.GameId, out var current)
                             && s.ScoringDay.Date <= current
                             && s.ScoringDay.Date > current.AddDays(-window));

                var historyByGame = history
                    .GroupBy(s => s.GameId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderBy(s => s.ScoringDay)
                              .Select(s => new ScoreHistoryEntryDto
                              {
                                  ScoringDay = s.ScoringDay.ToString("yyyy-MM-dd"),
                                  ScoreValue = s.ScoreValue
                              })
                              .ToList()
                    );

                foreach (var game in top)
                {
                    if (historyByGame.TryGetValue(game.GameId, out var gameHistory))
                        game.ScoreHistory = gameHistory;
                }
            }

            var dto = DtoMapper.ToUserProfileDto(user, top, _levelCalc, _time.GetUtcNow().UtcDateTime);
            dto.ScoresHidden = hiddenIds.Count > 0;

            _cache.Set(key, dto, _cacheTtl);

            return dto;
        }

        /// <summary>Ids of the user's submissions that are on their game's current scoring day.</summary>
        private async Task<HashSet<int>> GetCurrentDaySubmissionIdsAsync(int userId, Dictionary<int, DateTime> currentDayByGame)
        {
            if (currentDayByGame.Count == 0) return new HashSet<int>();

            var candidates = await _submissionRepo.GetByUserAndGamesAndDaysAsync(
                userId, currentDayByGame.Keys.ToList(), currentDayByGame.Values.Distinct().ToList());

            return candidates
                .Where(s => currentDayByGame.TryGetValue(s.GameId, out var day) && s.ScoringDay.Date == day)
                .Select(s => s.Id)
                .ToHashSet();
        }
    }
}
