using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;

namespace DailyChallenges.Services.Dashboard;

/// <summary>
/// Friends' recent plays: at most <see cref="MaxGames"/> games played in the last
/// <see cref="ActivityWindow"/>, most recently played first, each with its <see cref="PerGame"/> latest
/// friend submissions. The limits are applied in SQL.
/// </summary>
public sealed class DashboardFriendActivityBuilder(ISubmissionRepository submissions, TimeProvider time)
{
    public const int MaxGames = 10;
    public const int PerGame = 5;
    public const string FallbackUsername = "Friend";
    private const int UnknownUserId = 0;
    private static readonly TimeSpan ActivityWindow = TimeSpan.FromDays(7);

    public async Task<List<FriendActivityDto>> BuildAsync(IReadOnlyCollection<int> friendIds, DashboardTodaySnapshot today, CancellationToken ct)
    {
        if (friendIds.Count == 0) return new List<FriendActivityDto>();

        var since = time.GetUtcNow().UtcDateTime - ActivityWindow;
        var friendSubmissions = await submissions.GetRecentActivityByUsersAsync(new RecentActivityQuery(friendIds, since, MaxGames, PerGame), ct);

        return friendSubmissions
            .GroupBy(s => s.GameId)
            .Select(g => ToActivityDto(today, g.Key, g))
            .OrderByDescending(x => x.RecentSubmissions.First().SubmittedAt)
            .ThenBy(x => x.GameId)
            .Take(MaxGames)
            .ToList();
    }

    private static FriendActivityDto ToActivityDto(DashboardTodaySnapshot today, int gameId, IEnumerable<Submission> gameSubmissions)
    {
        var game = today.FindGame(gameId);
        return new FriendActivityDto
        {
            GameId = gameId,
            GameName = game?.Name ?? string.Empty,
            GameImageUrl = GameImageUrl.For(gameId, game?.ScreenshotContentType),
            GameUrl = game?.Url,
            IsFavorite = today.FavoriteGameIds.Contains(gameId),
            HasSubmittedForLatest = today.GamesViewerPlayedToday.Contains(gameId),
            RecentSubmissions = gameSubmissions
                .OrderByDescending(s => s.CreatedAt)
                .Take(PerGame)
                .Select(s => new FriendSubmissionItem
                {
                    UserId = s.UserId ?? UnknownUserId,
                    Username = !string.IsNullOrEmpty(s.Username) ? s.Username : FallbackUsername,
                    SubmittedAt = s.CreatedAt
                })
                .ToList()
        };
    }
}
