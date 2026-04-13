using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace DailyChallenges.Services
{
    public class UserProfileService : Contracts.IUserProfileService
    {
        private readonly IUserProfileRepository _repo;
        private readonly LevelCalculator _levelCalc;
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(60);

        public UserProfileService(IUserProfileRepository repo, LevelCalculator levelCalc, IMemoryCache cache)
        {
            _repo = repo;
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

            var dto = DtoMapper.ToUserProfileDto(user, top, _levelCalc);

            _cache.Set(key, dto, _cacheTtl);

            return dto;
        }
    }
}
