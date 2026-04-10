using DailyChallenges.Models;
using DailyChallenges.Repositories;

namespace DailyChallenges.Services
{
    public class FavoriteService : IFavoriteService
    {
        private readonly IFavoriteRepository _repo;
        private readonly IGameRepository _games;

        public FavoriteService(IFavoriteRepository repo, IGameRepository games)
        {
            _repo = repo;
            _games = games;
        }

        public async Task AddFavoriteAsync(int userId, int gameId)
        {
            var g = await _games.GetByIdAsync(gameId);
            if (g == null) throw new KeyNotFoundException("Game not found");

            var fav = new Favorite { UserId = userId, GameId = gameId };
            await _repo.AddAsync(fav);
        }

        public async Task RemoveFavoriteAsync(int userId, int gameId)
        {
            await _repo.RemoveAsync(userId, gameId);
        }

        public async Task<List<int>> GetFavoriteGameIdsForUserAsync(int userId)
        {
            return await _repo.GetFavoriteGameIdsForUserAsync(userId);
        }

        public async Task<bool> ExistsAsync(int userId, int gameId)
        {
            return await _repo.ExistsAsync(userId, gameId);
        }
    }
}
