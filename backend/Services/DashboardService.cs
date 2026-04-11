using DailyChallenges.Data;
using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly AppDbContext _db;
        private readonly IFavoriteRepository _favRepo;
        private readonly LevelCalculator _levelCalc;

        public DashboardService(AppDbContext db, IFavoriteRepository favRepo, LevelCalculator levelCalc)
        {
            _db = db;
            _favRepo = favRepo;
            _levelCalc = levelCalc;
        }

        public async Task<DashboardDataDto> GetDashboardDataAsync(int userId)
        {
            // 1. Resolve friend IDs
            var friendships = await _db.FriendRequests
                .Where(fr => (fr.SenderId == userId || fr.ReceiverId == userId)
                             && fr.Status == FriendRequestStatus.Accepted)
                .AsNoTracking()
                .ToListAsync();

            var friendIds = friendships
                .Select(fr => fr.SenderId == userId ? fr.ReceiverId : fr.SenderId)
                .ToHashSet();

            // 2. Load friends with last-active data
            var friendUsers = await _db.Users
                .Where(u => friendIds.Contains(u.Id))
                .AsNoTracking()
                .ToListAsync();

            var friendDtos = friendUsers
                .Select(u => DtoMapper.ToFriendDto(u, _levelCalc))
                .OrderByDescending(f => f.LastSubmissionAt)
                .ToList();

            // 3. Favorite game IDs for the current user (for IsFavorite flags)
            var favoriteIds = (await _favRepo.GetFavoriteGameIdsForUserAsync(userId)).ToHashSet();

            // 4. Load all games
            var games = await _db.Games.AsNoTracking().ToListAsync();

            // 5. Games with submissions today (per scoring day per game)
            //    Use each game's computed scoring day for accuracy.
            var gameScoringDays = games.Select(g =>
            {
                try { return (g.Id, Day: ScoringDayHelper.GetCurrentScoringDay(g.ResetTime, g.ResetTimezoneId).Date); }
                catch { return (g.Id, Day: DateTime.UtcNow.Date); }
            }).ToDictionary(x => x.Id, x => x.Day);

            // Gather the distinct days we need to query
            var distinctDays = gameScoringDays.Values.Distinct().ToList();

            // Bulk fetch submissions for today (all distinct scoring days)
            var todaySubmissions = await _db.Submissions
                .Where(s => distinctDays.Contains(s.ScoringDay))
                .AsNoTracking()
                .ToListAsync();

            // Group submissions by gameId — only for submissions that match that game's scoring day
            var submissionsByGame = todaySubmissions
                .Where(s => gameScoringDays.TryGetValue(s.GameId, out var d) && s.ScoringDay == d)
                .GroupBy(s => s.GameId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Current user's submitted game IDs for today
            var userSubmittedToday = todaySubmissions
                .Where(s => s.UserId == userId && gameScoringDays.TryGetValue(s.GameId, out var d) && s.ScoringDay == d)
                .Select(s => s.GameId)
                .ToHashSet();

            var recentGames = games
                .Where(g => submissionsByGame.ContainsKey(g.Id))
                .Select(g =>
                {
                    var subs = submissionsByGame[g.Id];
                    var dto = DtoMapper.ToDto(g);
                    dto.IsFavorite = favoriteIds.Contains(g.Id);
                    dto.HasSubmittedForLatest = userSubmittedToday.Contains(g.Id);
                    return new GameActivityDto
                    {
                        Game = dto,
                        TodayCount = subs.Count,
                        TodayUsernames = subs
                            .Select(s => !string.IsNullOrEmpty(s.Username) ? s.Username : "Anonymous")
                            .Distinct()
                            .Take(20)
                            .ToList()
                    };
                })
                .OrderByDescending(x => x.TodayCount)
                .Take(10)
                .ToList();

            // 6. Friend activity: games friends played in the last 7 days
            List<FriendActivityDto> friendActivity = new();
            if (friendIds.Count > 0)
            {
                var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
                var friendSubs = await _db.Submissions
                    .Where(s => s.UserId.HasValue && friendIds.Contains(s.UserId.Value) && s.CreatedAt >= sevenDaysAgo)
                    .AsNoTracking()
                    .ToListAsync();

                var gameIdMap = games.ToDictionary(g => g.Id);

                friendActivity = friendSubs
                    .GroupBy(s => s.GameId)
                    .Select(g =>
                    {
                        gameIdMap.TryGetValue(g.Key, out var game);
                        return new FriendActivityDto
                        {
                            GameId = g.Key,
                            GameName = game?.Name ?? string.Empty,
                            GameImageUrl = game?.ScreenshotData != null ? $"/api/games/{g.Key}/image" : null,
                            GameUrl = game?.Url,
                            IsFavorite = favoriteIds.Contains(g.Key),
                            HasSubmittedForLatest = userSubmittedToday.Contains(g.Key),
                            RecentSubmissions = g
                                .OrderByDescending(s => s.CreatedAt)
                                .Take(5)
                                .Select(s => new FriendSubmissionItem
                                {
                                    UserId = s.UserId ?? 0,
                                    Username = !string.IsNullOrEmpty(s.Username) ? s.Username : "Friend",
                                    SubmittedAt = s.CreatedAt
                                })
                                .ToList()
                        };
                    })
                    .OrderByDescending(x => x.RecentSubmissions.First().SubmittedAt)
                    .Take(10)
                    .ToList();
            }

            return new DashboardDataDto
            {
                RecentGames = recentGames,
                FriendActivity = friendActivity,
                Friends = friendDtos
            };
        }
    }
}
