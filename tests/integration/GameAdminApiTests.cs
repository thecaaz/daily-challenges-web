using System.Net;
using System.Text.Json;
using DailyChallenges.Data;
using DailyChallenges.IntegrationTests.Infrastructure;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.IntegrationTests;

/// <summary>
/// Game create/update over HTTP (H5, B3): UTC-only reset time, validation, clearable url/description,
/// and re-deriving stored scoring days when the reset time changes. Clock: 2026-06-15 10:00 UTC.
/// </summary>
public class GameAdminApiTests : IClassFixture<FixedClockApiFactory>
{
    private static readonly DateTime Now = FixedClockApiFactory.Now;
    private static readonly DateTime Today = Now.Date;

    private readonly FixedClockApiFactory _factory;

    public GameAdminApiTests(FixedClockApiFactory factory) => _factory = factory;

    private HttpClient Admin() => _factory.CreateAuthenticatedClient(1, "game-admin", isAdmin: true);

    private static MultipartFormDataContent Form(params (string Key, string Value)[] fields)
    {
        var form = new MultipartFormDataContent();
        foreach (var (k, v) in fields) form.Add(new StringContent(v), k);
        return form;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private async Task<int> CreateGameAsync(params (string Key, string Value)[] fields)
    {
        var res = await Admin().PostAsync("/api/games", Form(fields));
        return (await Json(res)).GetProperty("id").GetInt32();
    }

    private async Task<JsonElement> GetGameAsync(int id) => await Json(await _factory.CreateClient().GetAsync($"/api/games/{id}"));

    // ── Reset time: UTC "HH:mm" only ─────────────────────────────────────────

    [Fact]
    public async Task Get_then_put_round_trip_keeps_the_reset_time()
    {
        var id = await CreateGameAsync(("name", "RoundTrip"), ("resetTime", "22:30"), ("url", "https://example.com/a"));
        var game = await GetGameAsync(id);
        Assert.Equal("22:30", game.GetProperty("resetTime").GetString());

        // The edit form sends back what it got.
        var res = await Admin().PutAsync($"/api/games/{id}", Form(
            ("name", game.GetProperty("name").GetString()!),
            ("resetTime", game.GetProperty("resetTime").GetString()!),
            ("url", game.GetProperty("url").GetString()!),
            ("rankingMode", game.GetProperty("rankingMode").GetString()!)));
        await Json(res);

        var again = await GetGameAsync(id);
        Assert.Equal("22:30", again.GetProperty("resetTime").GetString());
        Assert.Equal("https://example.com/a", again.GetProperty("url").GetString());
    }

    [Fact]
    public async Task Legacy_timezone_offset_field_is_rejected_on_create_and_update()
    {
        var create = await Admin().PostAsync("/api/games", Form(("name", "Legacy"), ("resetTime", "01:00"), ("resetTimezoneOffsetMinutes", "-60")));
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        Assert.Contains("resetTimezoneOffsetMinutes", await create.Content.ReadAsStringAsync());

        var id = await CreateGameAsync(("name", "Legacy2"), ("resetTime", "01:00"));
        var update = await Admin().PutAsync($"/api/games/{id}", Form(("resetTime", "02:00"), ("resetTimezoneOffsetMinutes", "0")));
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        Assert.Equal("01:00", (await GetGameAsync(id)).GetProperty("resetTime").GetString());
    }

    [Theory]
    [InlineData("24:00")]
    [InlineData("1.02:00")]
    [InlineData("-05:00")]
    [InlineData("12:60")]
    [InlineData("noon")]
    [InlineData("10:00:00")]
    public async Task Invalid_reset_time_returns_400(string resetTime)
    {
        var res = await Admin().PostAsync("/api/games", Form(("name", "BadReset"), ("resetTime", resetTime)));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Invalid_ranking_mode_returns_400()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin().PostAsync("/api/games", Form(("name", "BadMode"), ("rankingMode", "fastest")))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin().PostAsync("/api/games", Form(("name", "BadMode"), ("rankingMode", "7")))).StatusCode);

        var id = await CreateGameAsync(("name", "Mode"), ("rankingMode", "LOWEST"));
        Assert.Equal("lowest", (await GetGameAsync(id)).GetProperty("rankingMode").GetString());
        var res = await Admin().PutAsync($"/api/games/{id}", Form(("rankingMode", "highest")));
        Assert.Equal("highest", (await Json(res)).GetProperty("rankingMode").GetString());
    }

    // ── Url / description ────────────────────────────────────────────────────

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/file")]
    [InlineData("example.com")]
    [InlineData("/relative/path")]
    public async Task Non_http_url_returns_400(string url)
    {
        var create = await Admin().PostAsync("/api/games", Form(("name", "BadUrl"), ("url", url)));
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);

        var id = await CreateGameAsync(("name", "BadUrl2"), ("url", "http://example.com"));
        var update = await Admin().PutAsync($"/api/games/{id}", Form(("url", url)));
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        Assert.Equal("http://example.com", (await GetGameAsync(id)).GetProperty("url").GetString());
    }

    [Fact]
    public async Task Empty_url_and_description_clear_them_and_omitted_fields_stay()
    {
        var id = await CreateGameAsync(("name", "Clear"), ("url", "https://example.com"), ("description", "Some text"));

        // Omitted: unchanged.
        var res = await Admin().PutAsync($"/api/games/{id}", Form(("name", "Clear renamed")));
        var game = await Json(res);
        Assert.Equal("https://example.com", game.GetProperty("url").GetString());
        Assert.Equal("Some text", game.GetProperty("description").GetString());

        // "": cleared (null values are omitted from the JSON).
        res = await Admin().PutAsync($"/api/games/{id}", Form(("url", ""), ("description", "")));
        game = await Json(res);
        Assert.False(game.TryGetProperty("url", out _));
        Assert.False(game.TryGetProperty("description", out _));
        Assert.Equal("Clear renamed", game.GetProperty("name").GetString());
    }

    // ── Reset change re-derives stored scoring days ──────────────────────────

    private sealed record ResetSeed(int GameId, int MovedSubId, int StaysSubId, int FinalizedSubId, int UserA, int UserB);

    /// <summary>
    /// Game with reset 00:00, day Today-2 finalized. User A played Today-1 20:00 and (optionally) Today 09:00;
    /// user B played Today 09:30. Plus a submission on the finalized day.
    /// </summary>
    private async Task<ResetSeed> SeedResetGameAsync(string name, bool userAPlaysToday)
    {
        ResetSeed seed = null!;
        await _factory.WithDbAsync(async db =>
        {
            var game = new Game { Name = name, ResetTime = TimeSpan.Zero };
            var a = new User { Username = name + "-a", PasswordHash = "x" };
            var b = new User { Username = name + "-b", PasswordHash = "x" };
            db.AddRange(game, a, b);
            await db.SaveChangesAsync();

            db.ScoringDayResults.Add(new ScoringDayResult { GameId = game.Id, ScoringDay = Today.AddDays(-2), ProcessedAt = Today.AddDays(-1) });
            var finalized = Sub(game.Id, a, Today.AddDays(-2).AddHours(20));
            var moved = Sub(game.Id, a, Today.AddDays(-1).AddHours(20));
            var stays = Sub(game.Id, b, Today.AddHours(9.5));
            db.Submissions.AddRange(finalized, moved, stays);
            if (userAPlaysToday) db.Submissions.Add(Sub(game.Id, a, Today.AddHours(9)));
            await db.SaveChangesAsync();

            db.XpEvents.Add(new XpEvent { UserId = a.Id, GameId = game.Id, SubmissionId = moved.Id, ScoringDay = moved.ScoringDay, Amount = 50, EventType = "submission" });
            db.XpEvents.Add(new XpEvent { UserId = a.Id, GameId = game.Id, SubmissionId = finalized.Id, ScoringDay = finalized.ScoringDay, Amount = 50, EventType = "submission" });
            await db.SaveChangesAsync();
            seed = new ResetSeed(game.Id, moved.Id, stays.Id, finalized.Id, a.Id, b.Id);
        });
        return seed;
    }

    private static Submission Sub(int gameId, User user, DateTime createdAt) => new()
    {
        GameId = gameId, UserId = user.Id, Username = user.Username, Score = "1", ScoreValue = 1,
        CreatedAt = createdAt, ScoringDay = createdAt.Date, ScreenshotData = new byte[] { 1 }, ScreenshotContentType = "image/png",
    };

    private async Task<DateTime> ScoringDayOf(int subId)
    {
        DateTime day = default;
        await _factory.WithDbAsync(async db => day = (await db.Submissions.AsNoTracking().SingleAsync(s => s.Id == subId)).ScoringDay.Date);
        return day;
    }

    private async Task<DateTime?> XpEventDayOf(int subId)
    {
        DateTime? day = null;
        await _factory.WithDbAsync(async db => day = (await db.XpEvents.AsNoTracking().SingleAsync(e => e.SubmissionId == subId)).ScoringDay);
        return day?.Date;
    }

    [Fact]
    public async Task Reset_change_re_derives_scoring_days_of_unfinalized_submissions_and_their_xp_events()
    {
        var seed = await SeedResetGameAsync("Rederive", userAPlaysToday: false);

        // 18:00 is an end-of-day reset: Today-1 20:00 now belongs to Today; Today 09:30 stays on Today.
        var res = await Admin().PutAsync($"/api/games/{seed.GameId}", Form(("resetTime", "18:00")));
        Assert.Equal("18:00", (await Json(res)).GetProperty("resetTime").GetString());

        Assert.Equal(Today, await ScoringDayOf(seed.MovedSubId));
        Assert.Equal(Today, await XpEventDayOf(seed.MovedSubId));
        Assert.Equal(Today, await ScoringDayOf(seed.StaysSubId));
        // Finalized days are never touched.
        Assert.Equal(Today.AddDays(-2), await ScoringDayOf(seed.FinalizedSubId));
        Assert.Equal(Today.AddDays(-2), await XpEventDayOf(seed.FinalizedSubId));
    }

    [Fact]
    public async Task Reset_change_that_would_merge_a_users_submissions_returns_409_and_changes_nothing()
    {
        var seed = await SeedResetGameAsync("Merge", userAPlaysToday: true);

        var res = await Admin().PutAsync($"/api/games/{seed.GameId}", Form(("resetTime", "18:00"), ("name", "Merge renamed")));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var game = await GetGameAsync(seed.GameId);
        Assert.Equal("00:00", game.GetProperty("resetTime").GetString());
        Assert.Equal("Merge", game.GetProperty("name").GetString());
        Assert.Equal(Today.AddDays(-1), await ScoringDayOf(seed.MovedSubId));
    }

    [Fact]
    public async Task Reset_change_into_a_finalized_day_returns_409()
    {
        var seed = await SeedResetGameAsync("IntoFinalized", userAPlaysToday: false);
        // Finalize Today-1 as well; 09:45 (start-of-day reset) would put Today 09:30 on Today-1.
        await _factory.WithDbAsync(async db =>
        {
            db.ScoringDayResults.Add(new ScoringDayResult { GameId = seed.GameId, ScoringDay = Today.AddDays(-1), ProcessedAt = Today });
            await db.SaveChangesAsync();
        });

        var res = await Admin().PutAsync($"/api/games/{seed.GameId}", Form(("resetTime", "09:45")));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal(Today, await ScoringDayOf(seed.StaysSubId));

        // 11:00 would also make the current day (Today-1) a finalized one.
        res = await Admin().PutAsync($"/api/games/{seed.GameId}", Form(("resetTime", "11:00")));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("00:00", (await GetGameAsync(seed.GameId)).GetProperty("resetTime").GetString());
    }
}
