using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Contracts;
using Microsoft.Extensions.Logging;

namespace DailyChallenges.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly TimeProvider _time;
        private readonly IFriendRepository _friendRepo;
        private readonly IGameRepository _gameRepo;
        private readonly ISubmissionRepository _submissionRepo;
        private readonly IXpEventRepository _xpEventRepo;
        private readonly IFavoriteRepository _favRepo;
        private readonly LevelCalculator _levelCalc;
        private readonly ILogger<DashboardService> _logger;

        public DashboardService(
            IFriendRepository friendRepo,
            IGameRepository gameRepo,
            ISubmissionRepository submissionRepo,
            IXpEventRepository xpEventRepo,
            IFavoriteRepository favRepo,
            LevelCalculator levelCalc,
            ILogger<DashboardService> logger, TimeProvider time)
        {
            _friendRepo = friendRepo;
            _gameRepo = gameRepo;
            _submissionRepo = submissionRepo;
            _xpEventRepo = xpEventRepo;
            _favRepo = favRepo;
            _levelCalc = levelCalc;
            _logger = logger;
            _time = time;
        }

        /// <summary>XP event types that count towards "XP earned today" (the streak bonus is part of "submission").</summary>
        private static readonly string[] TodayXpEventTypes = { "submission", "day_win", "submission_revoked" };

        public async Task<DashboardDataDto> GetDashboardDataAsync(int userId)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var friendUsers = await _friendRepo.GetFriendsAsync(userId);
            var friendIds = friendUsers.Select(u => u.Id).ToHashSet();
            var friendDtos = friendUsers
                .Select(u => DtoMapper.ToFriendDto(u, _levelCalc, now))
                .OrderByDescending(f => f.LastSubmissionAt)
                .ToList();

            var favoriteIds = (await _favRepo.GetFavoriteGameIdsForUserAsync(userId)).ToHashSet();
            var games = await _gameRepo.GetAllAsync();
            var gameScoringDays = BuildGameScoringDays(games, now);
            var distinctDays = gameScoringDays.Values.Distinct().ToList();

            var todaySubmissions = await _submissionRepo.GetByScoringDaysAsync(distinctDays);
            var submissionsByGame = todaySubmissions
                .Where(s => gameScoringDays.TryGetValue(s.GameId, out var d) && s.ScoringDay == d)
                .GroupBy(s => s.GameId)
                .ToDictionary(g => g.Key, g => g.ToList());
            var userSubmittedToday = todaySubmissions
                .Where(s => s.UserId == userId && gameScoringDays.TryGetValue(s.GameId, out var d) && s.ScoringDay == d)
                .Select(s => s.GameId)
                .ToHashSet();

            var recentGames = BuildRecentGames(games, submissionsByGame, userSubmittedToday, favoriteIds, now);

            var gameById = games.ToDictionary(g => g.Id);
            var friendActivity = await ComputeFriendActivityAsync(friendIds, userSubmittedToday, favoriteIds, gameById);
            // Each event counts only on its own game's current scoring day.
            var xpEarnedToday = await _xpEventRepo.SumAmountByUserAndGameDaysAsync(userId, gameScoringDays, TodayXpEventTypes);
            var userTodayRanks = ComputeUserRanks(userId, todaySubmissions, submissionsByGame, gameScoringDays, gameById, userSubmittedToday);
            // Real totals for the stat cards (RecentGames is capped at 10).
            var (activeGameCount, submissionCountToday) = await _submissionRepo.CountOnGameDaysAsync(gameScoringDays);

            return new DashboardDataDto
            {
                RecentGames = recentGames,
                FriendActivity = friendActivity,
                Friends = friendDtos,
                XpEarnedToday = xpEarnedToday,
                UserTodayRanks = userTodayRanks,
                ActiveGameCount = activeGameCount,
                SubmissionCountToday = submissionCountToday
            };
        }

        private Dictionary<int, DateTime> BuildGameScoringDays(List<Game> games, DateTime now)
        {
            return games.Select(g =>
            {
                try { return (g.Id, Day: ScoringDayHelper.GetCurrentScoringDay(g.ResetTime, now).Date); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to compute scoring day for game {GameId}; falling back to UTC.Date", g.Id);
                    return (g.Id, Day: now.Date);
                }
            }).ToDictionary(x => x.Id, x => x.Day);
        }

        private static List<GameActivityDto> BuildRecentGames(
            List<Game> games,
            Dictionary<int, List<Submission>> submissionsByGame,
            HashSet<int> userSubmittedToday,
            HashSet<int> favoriteIds,
            DateTime now)
        {
            return games
                .Where(g => submissionsByGame.ContainsKey(g.Id))
                .Select(g =>
                {
                    var subs = submissionsByGame[g.Id];
                    var dto = DtoMapper.ToDto(g, nowUtc: now);
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
        }

        /// <summary>Friend activity: at most this many games, each with at most this many recent submissions.</summary>
        private const int FriendActivityMaxGames = 10;
        private const int FriendActivityPerGame = 5;

        private async Task<List<FriendActivityDto>> ComputeFriendActivityAsync(
            HashSet<int> friendIds,
            HashSet<int> userSubmittedToday,
            HashSet<int> favoriteIds,
            Dictionary<int, Game> gameById)
        {
            if (friendIds.Count == 0) return new List<FriendActivityDto>();

            var sevenDaysAgo = _time.GetUtcNow().UtcDateTime.AddDays(-7);
            // Limited in SQL: the 10 most recently played games, 5 latest submissions each, ordered by game recency.
            var friendSubs = await _submissionRepo.GetRecentActivityByUsersAsync(
                friendIds, sevenDaysAgo, FriendActivityMaxGames, FriendActivityPerGame);

            return friendSubs
                .GroupBy(s => s.GameId)
                .Select(g =>
                {
                    gameById.TryGetValue(g.Key, out var game);
                    return new FriendActivityDto
                    {
                        GameId = g.Key,
                        GameName = game?.Name ?? string.Empty,
                        GameImageUrl = !string.IsNullOrEmpty(game?.ScreenshotContentType) ? $"/api/games/{g.Key}/image" : null,
                        GameUrl = game?.Url,
                        IsFavorite = favoriteIds.Contains(g.Key),
                        HasSubmittedForLatest = userSubmittedToday.Contains(g.Key),
                        RecentSubmissions = g
                            .OrderByDescending(s => s.CreatedAt)
                            .Take(FriendActivityPerGame)
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
                .ThenBy(x => x.GameId)
                .Take(FriendActivityMaxGames)
                .ToList();
        }

        private static List<UserTodayRankDto> ComputeUserRanks(
            int userId,
            List<Submission> todaySubmissions,
            Dictionary<int, List<Submission>> submissionsByGame,
            Dictionary<int, DateTime> gameScoringDays,
            Dictionary<int, Game> gameById,
            HashSet<int> userSubmittedToday)
        {
            var userSubsByGame = todaySubmissions
                .Where(s => s.UserId == userId
                         && gameScoringDays.TryGetValue(s.GameId, out var d)
                         && s.ScoringDay == d)
                .GroupBy(s => s.GameId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreatedAt).First());

            return userSubmittedToday
                .Where(gameId => userSubsByGame.ContainsKey(gameId) && userSubsByGame[gameId].ScoreValue.HasValue)
                .Select(gameId =>
                {
                    var userSub = userSubsByGame[gameId];
                    gameById.TryGetValue(gameId, out var game);
                    var gameSubs = submissionsByGame.ContainsKey(gameId) ? submissionsByGame[gameId] : new List<Submission>();
                    var mode = game?.RankingMode ?? RankingMode.Highest;
                    // Rank matches leaderboard ordering: best ScoreValue first, ties broken by earliest CreatedAt.
                    int rank = mode == RankingMode.Highest
                        ? gameSubs.Count(s =>
                            (s.ScoreValue ?? double.MinValue) > userSub.ScoreValue!.Value ||
                            (s.ScoreValue == userSub.ScoreValue && s.CreatedAt < userSub.CreatedAt)) + 1
                        : gameSubs.Count(s =>
                            (s.ScoreValue ?? double.MaxValue) < userSub.ScoreValue!.Value ||
                            (s.ScoreValue == userSub.ScoreValue && s.CreatedAt < userSub.CreatedAt)) + 1;
                    return new UserTodayRankDto
                    {
                        GameId = gameId,
                        GameName = game?.Name ?? string.Empty,
                        GameImageUrl = !string.IsNullOrEmpty(game?.ScreenshotContentType) ? $"/api/games/{gameId}/image" : null,
                        Score = userSub.Score,
                        Rank = rank,
                        TotalSubmissions = gameSubs.Count(s => s.ScoreValue.HasValue)
                    };
                })
                .OrderBy(r => r.Rank)
                .ToList();
        }
    }
}
