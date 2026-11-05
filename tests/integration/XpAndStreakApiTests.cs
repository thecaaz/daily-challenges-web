using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.IntegrationTests;

/// <summary>Admin XP adjustments, the dashboard's "XP earned today" and the effective streak (B6, B9). Clock: 2026-06-15 10:00 UTC.</summary>
public class XpAndStreakApiTests : IClassFixture<FixedClockApiFactory>
{
    private static readonly DateTime Today = FixedClockApiFactory.Now.Date;
    private readonly FixedClockApiFactory _factory;

    public XpAndStreakApiTests(FixedClockApiFactory factory) => _factory = factory;

    private async Task<User> SeedUserAsync(string name, long totalXp = 0, int streak = 0, DateTime? lastSubmissionAt = null)
    {
        var user = new User { Username = name, PasswordHash = "x", TotalXp = totalXp, Level = 1, Streak = streak, LastSubmissionAt = lastSubmissionAt };
        await _factory.WithDbAsync(async db => { db.Users.Add(user); await db.SaveChangesAsync(); });
        return user;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    // ── Admin adjustments ────────────────────────────────────────────────────

    [Fact]
    public async Task Admin_adjustment_clamps_at_zero_and_records_the_applied_delta()
    {
        var user = await SeedUserAsync("adjust-clamp", totalXp: 30);
        var admin = _factory.CreateAuthenticatedClient(user.Id + 1000, "admin", isAdmin: true);

        var dto = await Json(await admin.PostAsJsonAsync($"/api/admin/users/{user.Id}/xp", new { delta = -100, reason = "cheating" }));
        Assert.Equal(0, dto.GetProperty("totalXp").GetInt64());

        XpEvent ev = null!;
        await _factory.WithDbAsync(async db => ev = await db.XpEvents.AsNoTracking().SingleAsync(e => e.UserId == user.Id));
        Assert.Equal("admin_adjustment", ev.EventType);
        Assert.Equal(-30, ev.Amount);
        Assert.Contains("requested=-100", ev.Details);
    }

    [Fact]
    public async Task Admin_adjustment_updates_level_and_checks_level_achievements()
    {
        var user = await SeedUserAsync("adjust-level");
        var admin = _factory.CreateAuthenticatedClient(user.Id + 1000, "admin", isAdmin: true);

        var dto = await Json(await admin.PostAsJsonAsync($"/api/admin/users/{user.Id}/xp", new { delta = 2000, reason = "event prize" }));
        Assert.Equal(5, dto.GetProperty("level").GetInt32());

        List<string> unlocked = null!;
        await _factory.WithDbAsync(async db => unlocked = await db.UserAchievements.Where(a => a.UserId == user.Id).Select(a => a.AchievementId).ToListAsync());
        Assert.Contains("level_5", unlocked);
    }

    [Fact]
    public async Task Admin_adjustment_for_unknown_user_returns_404()
    {
        var admin = _factory.CreateAuthenticatedClient(999_999, "admin", isAdmin: true);
        var res = await admin.PostAsJsonAsync("/api/admin/users/999998/xp", new { delta = 10, reason = "x" });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ── Dashboard: XP earned today ───────────────────────────────────────────

    [Fact]
    public async Task Xp_earned_today_uses_each_games_own_scoring_day_and_counts_day_wins()
    {
        var user = await SeedUserAsync("xp-today");
        await _factory.WithDbAsync(async db =>
        {
            // At 10:00 UTC: game A (reset 00:00) is on Today, game B (reset 11:00) still on Today-1.
            var a = new Game { Name = "XpToday A", ResetTime = TimeSpan.Zero };
            var b = new Game { Name = "XpToday B", ResetTime = TimeSpan.FromHours(11) };
            db.Games.AddRange(a, b);
            await db.SaveChangesAsync();

            db.XpEvents.AddRange(
                new XpEvent { UserId = user.Id, GameId = a.Id, ScoringDay = Today, Amount = 50, EventType = "submission" },
                new XpEvent { UserId = user.Id, GameId = a.Id, ScoringDay = Today.AddDays(-1), Amount = 40, EventType = "submission" }, // A's yesterday
                new XpEvent { UserId = user.Id, GameId = b.Id, ScoringDay = Today.AddDays(-1), Amount = 30, EventType = "submission" },
                new XpEvent { UserId = user.Id, GameId = b.Id, ScoringDay = Today.AddDays(-1), Amount = 100, EventType = "day_win" },
                new XpEvent { UserId = user.Id, GameId = b.Id, ScoringDay = Today, Amount = 20, EventType = "submission" },     // B's tomorrow
                new XpEvent { UserId = user.Id, Amount = 500, EventType = "admin_adjustment" });
            await db.SaveChangesAsync();
        });

        var dashboard = await Json(await _factory.CreateAuthenticatedClient(user.Id, user.Username).GetAsync("/api/dashboard"));

        Assert.Equal(50 + 30 + 100, dashboard.GetProperty("xpEarnedToday").GetInt32());
    }

    // ── Effective streak ─────────────────────────────────────────────────────

    [Fact]
    public async Task Streak_reads_zero_after_a_gap_and_stays_while_alive()
    {
        var stale = await SeedUserAsync("streak-stale", streak: 25, lastSubmissionAt: Today.AddDays(-2).AddHours(23));
        var alive = await SeedUserAsync("streak-alive", streak: 4, lastSubmissionAt: Today.AddDays(-1).AddHours(7));

        var staleMe = await Json(await _factory.CreateAuthenticatedClient(stale.Id, stale.Username).GetAsync("/api/auth/me"));
        Assert.Equal(0, staleMe.GetProperty("streak").GetInt32());
        var staleProfile = await Json(await _factory.CreateClient().GetAsync($"/api/users/{stale.Id}/profile"));
        Assert.Equal(0, staleProfile.GetProperty("streak").GetInt32());

        var aliveMe = await Json(await _factory.CreateAuthenticatedClient(alive.Id, alive.Username).GetAsync("/api/auth/me"));
        Assert.Equal(4, aliveMe.GetProperty("streak").GetInt32());

        // Stored value untouched; it restarts properly on the next submission.
        await _factory.WithDbAsync(async db => Assert.Equal(25, (await db.Users.AsNoTracking().SingleAsync(u => u.Id == stale.Id)).Streak));
    }
}

/// <summary>League summaries use each game's current scoring day as the end of the window (B8).</summary>
public class LeagueSummaryWindowTests
{
    [Fact]
    public async Task Late_reset_game_keeps_todays_plays_in_the_summary()
    {
        await using var db = new TestDb();
        // 23:00 UTC with a 22:00 reset: the game's current scoring day is tomorrow's date.
        var now = new DateTime(2026, 6, 15, 23, 0, 0, DateTimeKind.Utc);
        var user = new User { Id = 1, Username = "late" };
        var game = new Game { Id = 1, Name = "Late", ResetTime = TimeSpan.FromHours(22) };
        db.Ctx.AddRange(user, game);
        db.Ctx.Leagues.Add(new League { Id = 1, OwnerId = 1, Name = "L", CreatedAt = now });
        db.Ctx.SaveChanges();
        db.Ctx.LeagueMembers.Add(new LeagueMember { LeagueId = 1, UserId = 1, Role = LeagueRole.Owner, JoinedAt = now });
        db.Ctx.Submissions.Add(new Submission
        {
            GameId = 1, UserId = 1, Username = "late", Score = "5", ScoreValue = 5,
            CreatedAt = now.AddMinutes(-30), ScoringDay = new DateTime(2026, 6, 16), ScreenshotContentType = "image/png",
        });
        db.Ctx.SaveChanges();

        var repo = new EfLeagueRepository(db.Ctx, new FixedTimeProvider(now));
        var (items, _) = await repo.GetLeagueGameSummariesAsync(1, requestingUserId: 1, days: 3);

        var summary = Assert.Single(items);
        Assert.Equal(1, summary.PlayCount);
        Assert.Equal(new List<int> { 0, 0, 1 }, summary.RecentPlays);
    }
}
