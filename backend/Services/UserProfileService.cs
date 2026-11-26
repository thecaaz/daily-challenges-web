using System.Security.Claims;
using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services.Contracts;
using Microsoft.Extensions.Caching.Memory;

namespace DailyChallenges.Services;

public sealed class UserProfileService(
    IUserProfileRepository profiles,
    ISubmissionRepository submissions,
    IGameRepository games,
    LevelCalculator levels,
    IMemoryCache cache,
    TimeProvider time) : IUserProfileService
{
    private const int SparklineDays = 30;
    private const string SparklineDayFormat = "yyyy-MM-dd";
    private const string AllDaysCacheScope = "all";
    private const string PastDaysCacheScope = "past";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    /// <summary>The user, the games' current scoring days and the submissions the viewer may not see.</summary>
    private sealed record ProfileScope(int UserId, Dictionary<int, DateTime> CurrentDayByGame, HashSet<int> HiddenSubmissionIds);

    /// <summary>
    /// Hidden-scores rule, simplified so the cache still works: each game's current scoring day is
    /// left out unless the viewer is the profile owner or an admin (stricter than the full rule).
    /// </summary>
    public async Task<UserProfileDto?> GetProfileAsync(int userId, int topGames, ClaimsPrincipal? viewer, CancellationToken ct)
    {
        var canSeeCurrentDay = viewer.GetUserId() == userId || viewer.IsAdmin();
        var cacheKey = CacheKey(userId, topGames, canSeeCurrentDay);
        if (cache.TryGetValue<UserProfileDto?>(cacheKey, out var cached)) return cached;

        var profile = await BuildProfileAsync(userId, topGames, canSeeCurrentDay, ct);
        if (profile == null) return null;

        cache.Set(cacheKey, profile, CacheTtl);
        return profile;
    }

    private static string CacheKey(int userId, int topGames, bool canSeeCurrentDay) =>
        $"UserProfile:{userId}:{topGames}:{(canSeeCurrentDay ? AllDaysCacheScope : PastDaysCacheScope)}";

    private async Task<UserProfileDto?> BuildProfileAsync(int userId, int topGames, bool canSeeCurrentDay, CancellationToken ct)
    {
        var user = await profiles.GetByIdAsync(userId, ct);
        if (user == null) return null;

        var now = time.GetUtcNow().UtcDateTime;
        var scope = await GetProfileScopeAsync(userId, canSeeCurrentDay, now, ct);
        var top = await profiles.GetMostPlayedGamesAsync(userId, topGames, scope.HiddenSubmissionIds, ct);
        await AttachScoreHistoryAsync(scope, top, now, ct);

        var profile = DtoMapper.ToUserProfileDto(user, UserProgression.Of(user, levels, now), top);
        profile.ScoresHidden = scope.HiddenSubmissionIds.Count > 0;
        return profile;
    }

    private async Task<ProfileScope> GetProfileScopeAsync(int userId, bool canSeeCurrentDay, DateTime now, CancellationToken ct)
    {
        var allGames = await games.GetAllAsync(ct);
        var currentDayByGame = allGames.ToDictionary(g => g.Id, g => ScoringDayCalculator.GetCurrentScoringDay(g.ResetTime, now).Date);
        var hiddenIds = canSeeCurrentDay ? new HashSet<int>() : await GetCurrentDaySubmissionIdsAsync(userId, currentDayByGame, ct);
        return new ProfileScope(userId, currentDayByGame, hiddenIds);
    }

    /// <summary>Ids of the user's submissions that are on their game's current scoring day.</summary>
    private async Task<HashSet<int>> GetCurrentDaySubmissionIdsAsync(int userId, Dictionary<int, DateTime> currentDayByGame, CancellationToken ct)
    {
        if (currentDayByGame.Count == 0) return new HashSet<int>();

        var candidates = await submissions.GetByUserAndGamesAndDaysAsync(
            userId, currentDayByGame.Keys.ToList(), currentDayByGame.Values.Distinct().ToList(), ct);

        return candidates
            .Where(s => currentDayByGame.TryGetValue(s.GameId, out var day) && s.ScoringDay.Date == day)
            .Select(s => s.Id)
            .ToHashSet();
    }

    /// <summary>
    /// Sparkline: the last 30 scoring days of each game, ending at that game's current scoring day
    /// (a late reset can make "today" tomorrow's UTC date). One query over the union of those days,
    /// then filtered per game.
    /// </summary>
    private async Task AttachScoreHistoryAsync(ProfileScope scope, List<UserGameStatDto> topGames, DateTime now, CancellationToken ct)
    {
        if (topGames.Count == 0) return;

        var gameIds = topGames.Select(g => g.GameId).ToList();
        var candidates = await submissions.GetByUserAndGamesAndDaysAsync(
            scope.UserId, gameIds, GetSparklineDayUnion(gameIds, scope.CurrentDayByGame, now), ct);

        var historyByGame = candidates
            .Where(s => !scope.HiddenSubmissionIds.Contains(s.Id))
            .Where(s => IsInSparklineWindow(s, scope.CurrentDayByGame))
            .GroupBy(s => s.GameId)
            .ToDictionary(g => g.Key, ToScoreHistory);

        foreach (var game in topGames)
        {
            if (historyByGame.TryGetValue(game.GameId, out var gameHistory))
                game.ScoreHistory = gameHistory;
        }
    }

    private static List<DateTime> GetSparklineDayUnion(List<int> gameIds, Dictionary<int, DateTime> currentDayByGame, DateTime now)
    {
        var gameDays = gameIds.Where(currentDayByGame.ContainsKey).Select(id => currentDayByGame[id]).ToList();
        var lastDay = gameDays.Count > 0 ? gameDays.Max() : now.Date;
        var firstDay = (gameDays.Count > 0 ? gameDays.Min() : now.Date).AddDays(-(SparklineDays - 1));
        return Enumerable.Range(0, (lastDay - firstDay).Days + 1).Select(i => firstDay.AddDays(i)).ToList();
    }

    private static bool IsInSparklineWindow(Submission submission, Dictionary<int, DateTime> currentDayByGame) =>
        currentDayByGame.TryGetValue(submission.GameId, out var current)
        && submission.ScoringDay.Date <= current
        && submission.ScoringDay.Date > current.AddDays(-SparklineDays);

    private static List<ScoreHistoryEntryDto> ToScoreHistory(IEnumerable<Submission> gameSubmissions) =>
        gameSubmissions
            .OrderBy(s => s.ScoringDay)
            .Select(s => new ScoreHistoryEntryDto
            {
                ScoringDay = s.ScoringDay.ToString(SparklineDayFormat),
                ScoreValue = s.ScoreValue
            })
            .ToList();
}
