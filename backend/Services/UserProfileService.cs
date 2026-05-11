using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace DailyChallenges.Services
{
    public class UserProfileService : Contracts.IUserProfileService
    {
        private readonly IUserProfileRepository _repo;
        private readonly ISubmissionRepository _submissionRepo;
        private readonly LevelCalculator _levelCalc;
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(60);

        public UserProfileService(IUserProfileRepository repo, ISubmissionRepository submissionRepo, LevelCalculator levelCalc, IMemoryCache cache)
        {
            _repo = repo;
            _submissionRepo = submissionRepo;
            _levelCalc = levelCalc;
            _cache = cache;
        }

        public async Task<UserProfileDto?> GetProfileAsync(int userId, int topGames = 10)
        {
            var key = $"UserProfile:{userId}:{topGames}";
            if (_cache.TryGetValue<UserProfileDto?>(key, out var cached)) return cached;

            var user = await _repo.GetByIdAsync(userId);
            if (user == null) return null;

            var top = await _repo.GetMostPlayedGamesAsync(userId, topGames);

            if (top.Count > 0)
            {
                var gameIds = top.Select(g => g.GameId).ToList();
                var today = DateTime.UtcNow.Date;
                var last30Days = Enumerable.Range(0, 30).Select(i => today.AddDays(-i)).ToList();

                var history = await _submissionRepo.GetByUserAndGamesAndDaysAsync(userId, gameIds, last30Days);

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

            var dto = DtoMapper.ToUserProfileDto(user, top, _levelCalc);

            _cache.Set(key, dto, _cacheTtl);

            return dto;
        }
    }
}
