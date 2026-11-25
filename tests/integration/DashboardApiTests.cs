using System.Text.Json;
using DailyChallenges.IntegrationTests.Infrastructure;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace DailyChallenges.IntegrationTests;

/// <summary>
/// Dashboard stat totals and friend activity (B9, F5). Clock: 2026-06-15 10:00 UTC.
/// Each test owns its factory, so the totals cover only what the test seeds.
/// </summary>
public class DashboardApiTests
{
    private static readonly DateTime Now = FixedClockApiFactory.Now;
    private static readonly DateTime Today = Now.Date;
    private static readonly DateTime Yesterday = Today.AddDays(-1);

    // One submission per user, game and scoring day is enforced, so repeated plays use distinct days.
    private static Submission Sub(Game game, User user, DateTime createdAt, DateTime scoringDay) => new()
    {
        GameId = game.Id,
        UserId = user.Id,
        Username = user.Username,
        Score = "10",
        ScoreValue = 10,
        CreatedAt = createdAt,
        ScoringDay = scoringDay,
        ScreenshotData = new byte[] { 1, 2, 3 },
        ScreenshotContentType = "image/png",
    };

    private static async Task<JsonElement> GetDashboardAsync(FixedClockApiFactory f, User user)
    {
        var res = await f.CreateAuthenticatedClient(user.Id, user.Username).GetAsync("/api/dashboard");
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    [Fact]
    public async Task Stat_totals_count_every_game_on_its_own_current_scoring_day_and_are_not_capped()
    {
        using var f = new FixedClockApiFactory();
        User viewer = null!;
        await f.WithDbAsync(async db =>
        {
            viewer = new User { Username = "dash-viewer", PasswordHash = "x" };
            var u1 = new User { Username = "dash-u1", PasswordHash = "x" };
            var u2 = new User { Username = "dash-u2", PasswordHash = "x" };
            // At 10:00 UTC: A (reset 00:00) is on Today, B (reset 11:00) still on Yesterday.
            var a = new Game { Name = "A", ResetTime = TimeSpan.Zero };
            var b = new Game { Name = "B", ResetTime = TimeSpan.FromHours(11) };
            var c = new Game { Name = "C (only yesterday)", ResetTime = TimeSpan.Zero };
            var many = Enumerable.Range(1, 12).Select(i => new Game { Name = $"Many {i}", ResetTime = TimeSpan.Zero }).ToList();
            db.AddRange(viewer, u1, u2, a, b, c);
            db.AddRange(many);
            await db.SaveChangesAsync();

            db.Submissions.AddRange(
                Sub(a, u1, Today.AddHours(1), Today),
                Sub(a, u2, Today.AddHours(2), Today),
                Sub(a, u1, Yesterday.AddHours(5), Yesterday),     // A's previous day
                Sub(b, u1, Yesterday.AddHours(12), Yesterday),    // B's current day
                Sub(b, u2, Today.AddHours(1), Today),             // B's next day (not current)
                Sub(c, u1, Yesterday.AddHours(3), Yesterday));    // C has nothing on its current day
            db.Submissions.AddRange(many.Select((g, i) => Sub(g, u1, Today.AddHours(1).AddMinutes(i), Today)));
            await db.SaveChangesAsync();
        });

        var dashboard = await GetDashboardAsync(f, viewer);

        Assert.Equal(2 + 12, dashboard.GetProperty("activeGameCount").GetInt32());
        Assert.Equal(2 + 1 + 12, dashboard.GetProperty("submissionCountToday").GetInt32());
        Assert.Equal(10, dashboard.GetProperty("recentGames").GetArrayLength()); // the list stays capped
    }

    [Fact]
    public async Task Stat_totals_are_zero_without_submissions()
    {
        using var f = new FixedClockApiFactory();
        var viewer = new User { Username = "dash-empty", PasswordHash = "x" };
        await f.WithDbAsync(async db =>
        {
            db.AddRange(viewer, new Game { Name = "Quiet", ResetTime = TimeSpan.Zero });
            await db.SaveChangesAsync();
        });

        var dashboard = await GetDashboardAsync(f, viewer);

        Assert.Equal(0, dashboard.GetProperty("activeGameCount").GetInt32());
        Assert.Equal(0, dashboard.GetProperty("submissionCountToday").GetInt32());
    }

    [Fact]
    public async Task Friend_activity_keeps_the_10_most_recent_games_with_5_latest_friend_submissions_each()
    {
        using var f = new FixedClockApiFactory();
        User viewer = null!, friend1 = null!, friend2 = null!;
        List<Game> games = null!;
        await f.WithDbAsync(async db =>
        {
            viewer = new User { Username = "fa-viewer", PasswordHash = "x" };
            friend1 = new User { Username = "fa-friend1", PasswordHash = "x" };
            friend2 = new User { Username = "fa-friend2", PasswordHash = "x" };
            var stranger = new User { Username = "fa-stranger", PasswordHash = "x" };
            var pending = new User { Username = "fa-pending", PasswordHash = "x" };
            games = Enumerable.Range(0, 12).Select(i => new Game { Name = $"FA {i}", ResetTime = TimeSpan.Zero }).ToList();
            db.AddRange(viewer, friend1, friend2, stranger, pending);
            db.AddRange(games);
            await db.SaveChangesAsync();

            db.FriendRequests.AddRange(
                new FriendRequest { SenderId = viewer.Id, ReceiverId = friend1.Id, Status = FriendRequestStatus.Accepted },
                new FriendRequest { SenderId = friend2.Id, ReceiverId = viewer.Id, Status = FriendRequestStatus.Accepted },
                new FriendRequest { SenderId = pending.Id, ReceiverId = viewer.Id, Status = FriendRequestStatus.Pending });

            // Game i was last played i hours ago (game 0 most recently), all inside the 7-day window.
            for (var i = 0; i < games.Count; i++)
                db.Submissions.Add(Sub(games[i], i % 2 == 0 ? friend1 : friend2, Now.AddHours(-i - 1), Today));

            // Game 0 gets 7 more, older friend submissions (only its 5 latest are listed).
            for (var k = 0; k < 7; k++)
                db.Submissions.Add(Sub(games[0], friend2, Now.AddDays(-2).AddMinutes(-k), Today.AddDays(-2 - k)));

            // Noise: a stranger and a pending "friend" playing game 11 very recently, and a friend play older than 7 days on game 10.
            db.Submissions.Add(Sub(games[11], stranger, Now.AddMinutes(-1), Today));
            db.Submissions.Add(Sub(games[11], pending, Now.AddMinutes(-2), Today));
            db.Submissions.Add(Sub(games[10], friend1, Now.AddDays(-8), Today.AddDays(-8)));
            await db.SaveChangesAsync();
        });

        var activity = (await GetDashboardAsync(f, viewer)).GetProperty("friendActivity").EnumerateArray().ToList();

        // Capped at 10 games, most recent first: games 0..9 (10 and 11 were played longest ago by friends).
        Assert.Equal(games.Take(10).Select(g => g.Id), activity.Select(a => a.GetProperty("gameId").GetInt32()));

        Assert.Equal(5, activity[0].GetProperty("recentSubmissions").GetArrayLength());
        Assert.Equal(Now.AddHours(-1), activity[0].GetProperty("recentSubmissions")[0].GetProperty("submittedAt").GetDateTime());

        foreach (var a in activity)
        {
            Assert.False(string.IsNullOrEmpty(a.GetProperty("gameName").GetString()));
            Assert.True(a.TryGetProperty("isFavorite", out _));
            Assert.True(a.TryGetProperty("hasSubmittedForLatest", out _));
            var subs = a.GetProperty("recentSubmissions").EnumerateArray().ToList();
            Assert.InRange(subs.Count, 1, 5);
            Assert.All(subs, s => Assert.Contains(s.GetProperty("userId").GetInt32(), new[] { friend1.Id, friend2.Id }));
            var times = subs.Select(s => s.GetProperty("submittedAt").GetDateTime()).ToList();
            Assert.Equal(times.OrderByDescending(t => t), times);
        }
    }

    [Fact]
    public async Task Recent_activity_query_limits_games_and_submissions_per_game()
    {
        using var f = new FixedClockApiFactory();
        User u1 = null!, u2 = null!;
        List<Game> games = null!;
        await f.WithDbAsync(async db =>
        {
            u1 = new User { Username = "ra-u1", PasswordHash = "x" };
            u2 = new User { Username = "ra-u2", PasswordHash = "x" };
            games = Enumerable.Range(0, 4).Select(i => new Game { Name = $"RA {i}", ResetTime = TimeSpan.Zero }).ToList();
            db.AddRange(u1, u2);
            db.AddRange(games);
            await db.SaveChangesAsync();

            // Game 0: 4 plays (newest overall); game 1: 1 play; game 2: 3 plays; game 3: oldest.
            for (var k = 0; k < 4; k++) db.Submissions.Add(Sub(games[0], k % 2 == 0 ? u1 : u2, Now.AddMinutes(-k), Today.AddDays(-k)));
            db.Submissions.Add(Sub(games[1], u1, Now.AddHours(-1), Today));
            for (var k = 0; k < 3; k++) db.Submissions.Add(Sub(games[2], u2, Now.AddHours(-2).AddMinutes(-k), Today.AddDays(-k)));
            db.Submissions.Add(Sub(games[3], u1, Now.AddHours(-3), Today));
            await db.SaveChangesAsync();
        });

        List<Submission> rows = null!;
        using (var scope = f.Services.CreateScope())
        {
            rows = await scope.ServiceProvider.GetRequiredService<ISubmissionRepository>()
                .GetRecentActivityByUsersAsync(new RecentActivityQuery(new[] { u1.Id, u2.Id }, Now.AddDays(-7), MaxGames: 3, PerGame: 2));
        }

        Assert.Equal(
            new[] { games[0].Id, games[0].Id, games[1].Id, games[2].Id, games[2].Id },
            rows.Select(r => r.GameId));
        Assert.Equal(new[] { Now, Now.AddMinutes(-1) }, rows.Take(2).Select(r => r.CreatedAt));
        Assert.All(rows, r => Assert.Empty(r.ScreenshotData));
    }

    private static Submission Scored(Game game, User? user, double? scoreValue, DateTime createdAt) => new()
    {
        GameId = game.Id,
        UserId = user?.Id,
        Username = user?.Username,
        Score = scoreValue?.ToString() ?? "n/a",
        ScoreValue = scoreValue,
        CreatedAt = createdAt,
        ScoringDay = Today,
    };

    [Fact]
    public async Task User_today_ranks_follow_leaderboard_order_with_earliest_tie_first_and_skip_unscored()
    {
        using var f = new FixedClockApiFactory();
        User viewer = null!;
        Game highest = null!, lowest = null!;
        await f.WithDbAsync(async db =>
        {
            viewer = new User { Username = "rank-viewer", PasswordHash = "x" };
            var u1 = new User { Username = "rank-u1", PasswordHash = "x" };
            var u2 = new User { Username = "rank-u2", PasswordHash = "x" };
            var u3 = new User { Username = "rank-u3", PasswordHash = "x" };
            highest = new Game { Name = "Highest", ResetTime = TimeSpan.Zero, ScreenshotContentType = "image/png" };
            lowest = new Game { Name = "Lowest", ResetTime = TimeSpan.Zero, RankingMode = RankingMode.Lowest };
            var unscored = new Game { Name = "Unscored", ResetTime = TimeSpan.Zero };
            db.AddRange(viewer, u1, u2, u3, highest, lowest, unscored);
            await db.SaveChangesAsync();

            db.Submissions.AddRange(
                Scored(highest, u1, 100, Today.AddHours(1)),
                Scored(highest, u2, 50, Today.AddMinutes(90)),
                Scored(highest, viewer, 50, Today.AddHours(2)),
                Scored(highest, u3, null, Today.AddMinutes(30)),
                Scored(lowest, u1, 10, Today.AddHours(3)),
                Scored(lowest, viewer, 20, Today.AddHours(1)),
                Scored(lowest, u3, null, Today.AddMinutes(30)),
                Scored(unscored, viewer, null, Today.AddHours(1)));
            await db.SaveChangesAsync();
        });

        var ranks = (await GetDashboardAsync(f, viewer)).GetProperty("userTodayRanks").EnumerateArray().ToList();

        Assert.Equal(new[] { lowest.Id, highest.Id }, ranks.Select(r => r.GetProperty("gameId").GetInt32()));
        Assert.Equal(new[] { 2, 3 }, ranks.Select(r => r.GetProperty("rank").GetInt32()));
        Assert.Equal(new[] { 2, 3 }, ranks.Select(r => r.GetProperty("totalSubmissions").GetInt32()));
        Assert.Equal(new[] { "20", "50" }, ranks.Select(r => r.GetProperty("score").GetString()));
        Assert.Equal(new[] { "Lowest", "Highest" }, ranks.Select(r => r.GetProperty("gameName").GetString()));
        Assert.False(ranks[0].TryGetProperty("gameImageUrl", out _));
        Assert.Equal($"/api/games/{highest.Id}/image", ranks[1].GetProperty("gameImageUrl").GetString());
    }

    [Fact]
    public async Task Recent_games_list_anonymous_players_once_and_flag_the_viewers_own_submissions()
    {
        using var f = new FixedClockApiFactory();
        User viewer = null!;
        Game played = null!, other = null!;
        await f.WithDbAsync(async db =>
        {
            viewer = new User { Username = "recent-viewer", PasswordHash = "x" };
            var u1 = new User { Username = "recent-u1", PasswordHash = "x" };
            played = new Game { Name = "Played", ResetTime = TimeSpan.Zero };
            other = new Game { Name = "Other", ResetTime = TimeSpan.Zero };
            db.AddRange(viewer, u1, played, other);
            await db.SaveChangesAsync();

            db.Favorites.Add(new Favorite { UserId = viewer.Id, GameId = other.Id });
            db.Submissions.AddRange(
                Scored(played, viewer, 1, Today.AddHours(1)),
                Scored(played, null, 2, Today.AddHours(2)),
                Scored(played, null, 3, Today.AddHours(3)),
                Scored(other, u1, 4, Today.AddHours(1)));
            await db.SaveChangesAsync();
        });

        var recent = (await GetDashboardAsync(f, viewer)).GetProperty("recentGames").EnumerateArray().ToList();

        Assert.Equal(new[] { played.Id, other.Id }, recent.Select(r => r.GetProperty("game").GetProperty("id").GetInt32()));
        Assert.Equal(new[] { 3, 1 }, recent.Select(r => r.GetProperty("todayCount").GetInt32()));
        Assert.Equal(new[] { "recent-viewer", "Anonymous" },
            recent[0].GetProperty("todayUsernames").EnumerateArray().Select(u => u.GetString()));
        Assert.True(recent[0].GetProperty("game").GetProperty("hasSubmittedForLatest").GetBoolean());
        Assert.False(recent[0].GetProperty("game").GetProperty("isFavorite").GetBoolean());
        Assert.False(recent[1].GetProperty("game").GetProperty("hasSubmittedForLatest").GetBoolean());
        Assert.True(recent[1].GetProperty("game").GetProperty("isFavorite").GetBoolean());
    }
}
