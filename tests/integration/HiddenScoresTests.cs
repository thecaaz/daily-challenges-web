using System.Net;
using System.Text.Json;
using DailyChallenges.IntegrationTests.Infrastructure;
using DailyChallenges.Models;
using DailyChallenges.Services;
using DailyChallenges.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DailyChallenges.IntegrationTests;

/// <summary>An ApiFactory whose clock is fixed, so "the current scoring day" is deterministic.</summary>
public sealed class FixedClockApiFactory : ApiFactory
{
    public static readonly DateTime Now = new(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

    public FixedTimeProvider Clock { get; } = new(Now);

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(Clock);
    }
}

/// <summary>
/// Seeds one game (reset 00:00 UTC, highest wins) with two submissions on the current scoring day
/// (other=100, submitter=50) and two on the previous day (other=70, submitter=80), plus a league
/// with every user as a member.
/// </summary>
public sealed class HiddenScoresFixture : IAsyncLifetime
{
    public static readonly DateTime Today = FixedClockApiFactory.Now.Date;
    public static readonly DateTime Yesterday = Today.AddDays(-1);
    public static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 };

    public FixedClockApiFactory Factory { get; } = new();

    public User Admin { get; } = new() { Username = "hs-admin", PasswordHash = "x", IsAdmin = true };
    public User Submitter { get; } = new() { Username = "hs-submitter", PasswordHash = "x" };
    public User Other { get; } = new() { Username = "hs-other", PasswordHash = "x" };
    public User NonSubmitter { get; } = new() { Username = "hs-nonsubmitter", PasswordHash = "x" };

    public Game Game { get; } = new() { Name = "Hidden Game", ResetTime = TimeSpan.Zero, RankingMode = RankingMode.Highest };
    public League League { get; private set; } = null!;

    public Submission OtherToday { get; private set; } = null!;
    public Submission SubmitterToday { get; private set; } = null!;
    public Submission OtherYesterday { get; private set; } = null!;
    public Submission SubmitterYesterday { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Factory.WithDbAsync(async db =>
        {
            db.Users.AddRange(Admin, Submitter, Other, NonSubmitter);
            db.Games.Add(Game);
            await db.SaveChangesAsync();

            OtherToday = Sub(Other, "100", 100, Today.AddHours(8));
            SubmitterToday = Sub(Submitter, "50", 50, Today.AddHours(9));
            OtherYesterday = Sub(Other, "70", 70, Yesterday.AddHours(8));
            SubmitterYesterday = Sub(Submitter, "80", 80, Yesterday.AddHours(9));
            db.Submissions.AddRange(OtherToday, SubmitterToday, OtherYesterday, SubmitterYesterday);

            League = new League { Name = "Hidden League", OwnerId = Other.Id, CreatedAt = Yesterday };
            db.Leagues.Add(League);
            await db.SaveChangesAsync();

            db.LeagueMembers.AddRange(
                new LeagueMember { LeagueId = League.Id, UserId = Other.Id, Role = LeagueRole.Owner, JoinedAt = Yesterday },
                new LeagueMember { LeagueId = League.Id, UserId = Submitter.Id, Role = LeagueRole.Member, JoinedAt = Yesterday },
                new LeagueMember { LeagueId = League.Id, UserId = NonSubmitter.Id, Role = LeagueRole.Member, JoinedAt = Yesterday },
                new LeagueMember { LeagueId = League.Id, UserId = Admin.Id, Role = LeagueRole.Member, JoinedAt = Yesterday });
            await db.SaveChangesAsync();
        });
    }

    private Submission Sub(User user, string score, double value, DateTime createdAt) => new()
    {
        GameId = Game.Id,
        UserId = user.Id,
        Username = user.Username,
        Score = score,
        ScoreValue = value,
        CreatedAt = createdAt,
        ScoringDay = createdAt.Date,
        ScreenshotData = Png,
        ScreenshotContentType = "image/png",
    };

    public Task DisposeAsync()
    {
        Factory.Dispose();
        return Task.CompletedTask;
    }
}

public enum Viewer { Anonymous, NonSubmitter, Submitter, Admin, Owner }

/// <summary>
/// The hidden-scores rule (H2) on every read path: scores of the current scoring day are visible only
/// to admins and to users who submitted for that day; past days are visible to everyone.
/// </summary>
public class HiddenScoresTests : IClassFixture<HiddenScoresFixture>
{
    private readonly HiddenScoresFixture _fx;

    public HiddenScoresTests(HiddenScoresFixture fx) => _fx = fx;

    private static string Day(DateTime d) => d.ToString("yyyy-MM-dd");

    private HttpClient Client(Viewer viewer) => viewer switch
    {
        Viewer.Anonymous => _fx.Factory.CreateClient(),
        Viewer.NonSubmitter => _fx.Factory.CreateAuthenticatedClient(_fx.NonSubmitter.Id, _fx.NonSubmitter.Username),
        Viewer.Submitter => _fx.Factory.CreateAuthenticatedClient(_fx.Submitter.Id, _fx.Submitter.Username),
        Viewer.Admin => _fx.Factory.CreateAuthenticatedClient(_fx.Admin.Id, _fx.Admin.Username, isAdmin: true),
        Viewer.Owner => _fx.Factory.CreateAuthenticatedClient(_fx.Other.Id, _fx.Other.Username),
        _ => throw new ArgumentOutOfRangeException(nameof(viewer)),
    };

    /// <summary>Viewers who may see the current day: they submitted for it, or they are an admin.</summary>
    private static bool CanSeeToday(Viewer v) => v is Viewer.Submitter or Viewer.Admin or Viewer.Owner;

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string url, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var res = await client.GetAsync(url);
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"GET {url}: expected {expected}, got {res.StatusCode}: {body}");
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    /// <summary>Present and not null (null properties are omitted from the JSON anyway).</summary>
    private static bool Has(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind != JsonValueKind.Null;

    private static void AssertMasked(JsonElement item)
    {
        Assert.True(Has(item, "id") || Has(item, "userId"));
        Assert.True(Has(item, "username"));
        Assert.False(Has(item, "score"), $"score should be hidden: {item}");
        Assert.False(Has(item, "scoreValue"), $"scoreValue should be hidden: {item}");
        Assert.False(Has(item, "rank"), $"rank should be hidden: {item}");
        Assert.False(Has(item, "screenshotUrl"), $"screenshotUrl should be hidden: {item}");
    }

    private static bool IsSigned(string url) => url.Contains("?exp=") && url.Contains("&sig=");

    // ── GET /api/submissions/game/{id}?scoringDay=<today> ────────────────────

    [Theory]
    [InlineData(Viewer.Anonymous)]
    [InlineData(Viewer.NonSubmitter)]
    [InlineData(Viewer.Submitter)]
    [InlineData(Viewer.Admin)]
    public async Task Submissions_for_the_current_day_are_masked_unless_the_viewer_may_see_them(Viewer viewer)
    {
        var page = await GetJsonAsync(Client(viewer), $"/api/submissions/game/{_fx.Game.Id}?scoringDay={Day(HiddenScoresFixture.Today)}");
        var items = page.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal(!CanSeeToday(viewer), page.GetProperty("scoresHidden").GetBoolean());

        if (!CanSeeToday(viewer))
        {
            Assert.All(items, AssertMasked);
            Assert.All(items, i => Assert.Equal(Day(HiddenScoresFixture.Today), i.GetProperty("scoringDay").GetString()));
            return;
        }

        var top = items.Single(i => i.GetProperty("id").GetInt32() == _fx.OtherToday.Id);
        Assert.Equal("100", top.GetProperty("score").GetString());
        Assert.Equal(1, top.GetProperty("rank").GetInt32());
        Assert.True(top.GetProperty("isDayWinner").GetBoolean());
        Assert.All(items, i => Assert.True(IsSigned(i.GetProperty("screenshotUrl").GetString()!)));
    }

    // ── GET /api/submissions/game/{id} (no day) ─────────────────────────────

    [Theory]
    [InlineData(Viewer.Anonymous, false)]
    [InlineData(Viewer.NonSubmitter, false)]
    [InlineData(Viewer.Submitter, true)]
    // As before: without a day, today is listed only for users who submitted (admins included).
    [InlineData(Viewer.Admin, false)]
    public async Task Submissions_without_a_day_leave_out_today_for_non_submitters(Viewer viewer, bool includesToday)
    {
        var page = await GetJsonAsync(Client(viewer), $"/api/submissions/game/{_fx.Game.Id}");
        var items = page.GetProperty("items").EnumerateArray().ToList();
        var ids = items.Select(i => i.GetProperty("id").GetInt32()).ToHashSet();

        Assert.False(page.GetProperty("scoresHidden").GetBoolean());
        Assert.Contains(_fx.OtherYesterday.Id, ids);
        Assert.Contains(_fx.SubmitterYesterday.Id, ids);
        Assert.Equal(includesToday, ids.Contains(_fx.OtherToday.Id));
        Assert.Equal(includesToday ? 4 : 2, items.Count);

        // Past-day screenshots need no signature; current-day ones are signed.
        foreach (var item in items)
        {
            var url = item.GetProperty("screenshotUrl").GetString()!;
            var isToday = item.GetProperty("scoringDay").GetString() == Day(HiddenScoresFixture.Today);
            Assert.Equal(isToday, IsSigned(url));
        }
    }

    // ── GET /api/submissions/game/{id}/winner ────────────────────────────────

    [Theory]
    [InlineData(Viewer.Anonymous)]
    [InlineData(Viewer.NonSubmitter)]
    [InlineData(Viewer.Submitter)]
    [InlineData(Viewer.Admin)]
    public async Task Winner_without_a_day_is_the_previous_days_winner(Viewer viewer)
    {
        var winner = await GetJsonAsync(Client(viewer), $"/api/submissions/game/{_fx.Game.Id}/winner");

        Assert.Equal(_fx.SubmitterYesterday.Id, winner.GetProperty("id").GetInt32());
        Assert.Equal("80", winner.GetProperty("score").GetString());
        Assert.Equal(Day(HiddenScoresFixture.Yesterday), winner.GetProperty("scoringDay").GetString());
        Assert.True(winner.GetProperty("isDayWinner").GetBoolean());
    }

    [Theory]
    [InlineData(Viewer.Anonymous)]
    [InlineData(Viewer.NonSubmitter)]
    [InlineData(Viewer.Submitter)]
    [InlineData(Viewer.Admin)]
    public async Task Winner_of_the_current_day_is_hidden_unless_the_viewer_may_see_it(Viewer viewer)
    {
        var url = $"/api/submissions/game/{_fx.Game.Id}/winner?scoringDay={Day(HiddenScoresFixture.Today)}";

        if (!CanSeeToday(viewer))
        {
            var res = await Client(viewer).GetAsync(url);
            Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
            return;
        }

        var winner = await GetJsonAsync(Client(viewer), url);
        Assert.Equal(_fx.OtherToday.Id, winner.GetProperty("id").GetInt32());
        Assert.Equal("100", winner.GetProperty("score").GetString());
        Assert.True(IsSigned(winner.GetProperty("screenshotUrl").GetString()!));
    }

    // ── GET /api/submissions/{id} ─────────────────────────────────────────────

    [Theory]
    [InlineData(Viewer.Anonymous)]
    [InlineData(Viewer.NonSubmitter)]
    [InlineData(Viewer.Submitter)]
    [InlineData(Viewer.Admin)]
    [InlineData(Viewer.Owner)]
    public async Task Submission_by_id_on_the_current_day_is_masked_unless_the_viewer_may_see_it(Viewer viewer)
    {
        var dto = await GetJsonAsync(Client(viewer), $"/api/submissions/{_fx.OtherToday.Id}");

        Assert.Equal(_fx.OtherToday.Id, dto.GetProperty("id").GetInt32());
        Assert.Equal(Day(HiddenScoresFixture.Today), dto.GetProperty("scoringDay").GetString());

        if (!CanSeeToday(viewer))
        {
            AssertMasked(dto);
            Assert.False(dto.GetProperty("isDayWinner").GetBoolean());
            return;
        }

        Assert.Equal("100", dto.GetProperty("score").GetString());
        Assert.Equal(1, dto.GetProperty("rank").GetInt32());
        Assert.True(dto.GetProperty("isDayWinner").GetBoolean());
        Assert.True(IsSigned(dto.GetProperty("screenshotUrl").GetString()!));
    }

    [Theory]
    [InlineData(Viewer.Anonymous)]
    [InlineData(Viewer.NonSubmitter)]
    public async Task Submission_by_id_on_a_past_day_is_visible_with_rank_and_winner_flag(Viewer viewer)
    {
        var dto = await GetJsonAsync(Client(viewer), $"/api/submissions/{_fx.OtherYesterday.Id}");

        Assert.Equal("70", dto.GetProperty("score").GetString());
        Assert.Equal(2, dto.GetProperty("rank").GetInt32());
        Assert.False(dto.GetProperty("isDayWinner").GetBoolean());
        Assert.Equal(Day(HiddenScoresFixture.Yesterday), dto.GetProperty("scoringDay").GetString());
        Assert.False(IsSigned(dto.GetProperty("screenshotUrl").GetString()!));
    }

    // ── GET /api/submissions/{id}/screenshot ─────────────────────────────────

    [Fact]
    public async Task Signed_current_day_screenshot_url_works_without_a_token()
    {
        var dto = await GetJsonAsync(Client(Viewer.Submitter), $"/api/submissions/{_fx.OtherToday.Id}");
        var url = dto.GetProperty("screenshotUrl").GetString()!;

        // <img> requests carry no Bearer token.
        var res = await _fx.Factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("image/png", res.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HiddenScoresFixture.Png, await res.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [InlineData(Viewer.Anonymous)]
    [InlineData(Viewer.Submitter)]
    [InlineData(Viewer.Admin)]
    public async Task Current_day_screenshot_without_a_signature_is_forbidden(Viewer viewer)
    {
        var res = await Client(viewer).GetAsync($"/api/submissions/{_fx.OtherToday.Id}/screenshot");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Current_day_screenshot_with_a_bad_signature_is_forbidden()
    {
        var id = _fx.OtherToday.Id;
        var signer = _fx.Factory.Services.GetRequiredService<IScreenshotUrlSigner>();
        var now = new DateTimeOffset(FixedClockApiFactory.Now).ToUnixTimeSeconds();
        var client = _fx.Factory.CreateClient();

        var validExp = now + 600;
        var validSig = signer.ComputeSignature(id, validExp);
        var tampered = (validSig[0] == '0' ? "1" : "0") + validSig[1..];
        var expiredExp = now - 1;

        var cases = new[]
        {
            $"exp={validExp}&sig={tampered}",                                      // tampered signature
            $"exp={validExp + 1}&sig={validSig}",                                  // tampered expiry
            $"exp={expiredExp}&sig={signer.ComputeSignature(id, expiredExp)}",     // expired
            $"exp={validExp}&sig={signer.ComputeSignature(_fx.SubmitterToday.Id, validExp)}", // another submission's signature
            $"exp={validExp}&sig=not-hex",
            $"exp={validExp}",
            $"sig={validSig}",
        };

        foreach (var query in cases)
        {
            var res = await client.GetAsync($"/api/submissions/{id}/screenshot?{query}");
            Assert.True(res.StatusCode == HttpStatusCode.Forbidden, $"{query}: expected 403, got {res.StatusCode}");
        }

        // Sanity check: the untampered pair works.
        var ok = await client.GetAsync($"/api/submissions/{id}/screenshot?exp={validExp}&sig={validSig}");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task Past_day_screenshot_needs_no_signature()
    {
        var res = await _fx.Factory.CreateClient().GetAsync($"/api/submissions/{_fx.OtherYesterday.Id}/screenshot");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(HiddenScoresFixture.Png, await res.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Signed_url_stops_working_after_it_expires()
    {
        var signer = _fx.Factory.Services.GetRequiredService<IScreenshotUrlSigner>();
        var url = signer.SignedUrl(_fx.OtherToday.Id);
        var exp = long.Parse(url.Split("exp=")[1].Split('&')[0]);
        var now = new DateTimeOffset(FixedClockApiFactory.Now).ToUnixTimeSeconds();

        // Valid for (roughly) an hour.
        Assert.InRange(exp - now, (long)TimeSpan.FromMinutes(45).TotalSeconds, (long)TimeSpan.FromHours(1).TotalSeconds);
        Assert.True(signer.IsValid(_fx.OtherToday.Id, exp, url.Split("sig=")[1]));
        Assert.False(signer.IsValid(_fx.OtherToday.Id, now, url.Split("sig=")[1]));
    }

    // ── GET /api/games/{id}/highscore ────────────────────────────────────────

    [Theory]
    [InlineData(Viewer.Anonymous)]
    [InlineData(Viewer.NonSubmitter)]
    [InlineData(Viewer.Submitter)]
    [InlineData(Viewer.Admin)]
    public async Task Highscore_leaves_out_the_current_day_unless_the_viewer_may_see_it(Viewer viewer)
    {
        var res = await GetJsonAsync(Client(viewer), $"/api/games/{_fx.Game.Id}/highscore");
        var top = res.GetProperty("top").EnumerateArray().ToList();
        var ids = top.Select(t => t.GetProperty("id").GetInt32()).ToList();

        if (!CanSeeToday(viewer))
        {
            Assert.Equal(new[] { _fx.SubmitterYesterday.Id, _fx.OtherYesterday.Id }, ids);
            Assert.Equal("80", res.GetProperty("highscore").GetProperty("score").GetString());
            return;
        }

        Assert.Equal(new[] { _fx.OtherToday.Id, _fx.SubmitterYesterday.Id, _fx.OtherYesterday.Id, _fx.SubmitterToday.Id }, ids);
        Assert.Equal(new[] { 1, 2, 3, 4 }, top.Select(t => t.GetProperty("rank").GetInt32()));
        Assert.True(IsSigned(top[0].GetProperty("screenshotUrl").GetString()!));
        Assert.False(IsSigned(top[1].GetProperty("screenshotUrl").GetString()!));
    }

    // ── GET /api/users/{id}/profile ──────────────────────────────────────────

    [Theory]
    [InlineData(Viewer.Anonymous)]
    [InlineData(Viewer.NonSubmitter)]
    // Stricter than the full rule (keeps the cache): only the owner and admins see the current day.
    [InlineData(Viewer.Submitter)]
    [InlineData(Viewer.Admin)]
    [InlineData(Viewer.Owner)]
    public async Task Profile_leaves_out_the_current_day_unless_the_viewer_is_the_owner_or_an_admin(Viewer viewer)
    {
        var profile = await GetJsonAsync(Client(viewer), $"/api/users/{_fx.Other.Id}/profile");
        var game = profile.GetProperty("topGames").EnumerateArray().Single();
        var history = game.GetProperty("scoreHistory").EnumerateArray().Select(h => h.GetProperty("scoringDay").GetString()).ToList();
        var seesToday = viewer is Viewer.Owner or Viewer.Admin;

        Assert.Equal(!seesToday, profile.GetProperty("scoresHidden").GetBoolean());
        Assert.Equal(seesToday ? 2 : 1, game.GetProperty("plays").GetInt32());
        Assert.Equal(seesToday ? 100 : 70, game.GetProperty("highestScore").GetDouble());
        Assert.Equal(seesToday ? _fx.OtherToday.Id : _fx.OtherYesterday.Id, game.GetProperty("bestSubmissionId").GetInt32());
        Assert.Equal(seesToday, history.Contains(Day(HiddenScoresFixture.Today)));
    }

    [Fact]
    public async Task My_profile_shows_the_current_day()
    {
        var profile = await GetJsonAsync(Client(Viewer.Owner), "/api/users/me/profile");

        Assert.False(profile.GetProperty("scoresHidden").GetBoolean());
        Assert.Equal(2, profile.GetProperty("topGames")[0].GetProperty("plays").GetInt32());
    }

    // ── GET /api/leagues/{id}/leaderboard ────────────────────────────────────

    [Fact]
    public async Task League_leaderboard_requires_a_login()
    {
        var res = await Client(Viewer.Anonymous).GetAsync($"/api/leagues/{_fx.League.Id}/leaderboard?gameId={_fx.Game.Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Theory]
    [InlineData(Viewer.NonSubmitter)]
    [InlineData(Viewer.Submitter)]
    [InlineData(Viewer.Admin)]
    public async Task League_leaderboard_defaults_to_the_current_day_and_masks_it_unless_visible(Viewer viewer)
    {
        var lb = await GetJsonAsync(Client(viewer), $"/api/leagues/{_fx.League.Id}/leaderboard?gameId={_fx.Game.Id}");
        var entries = lb.GetProperty("entries").EnumerateArray().ToList();

        Assert.Equal(Day(HiddenScoresFixture.Today), lb.GetProperty("scoringDay").GetString());
        Assert.Equal(2, entries.Count);
        Assert.Equal(!CanSeeToday(viewer), lb.GetProperty("scoresHidden").GetBoolean());

        if (!CanSeeToday(viewer))
        {
            Assert.All(entries, e =>
            {
                AssertMasked(e);
                Assert.False(Has(e, "submissionId"));
            });
            // Ordered by name, not by score.
            Assert.Equal(new[] { _fx.Other.Username, _fx.Submitter.Username },
                entries.Select(e => e.GetProperty("username").GetString()));
            return;
        }

        Assert.Equal(_fx.Other.Id, entries[0].GetProperty("userId").GetInt32());
        Assert.Equal("100", entries[0].GetProperty("score").GetString());
        Assert.Equal(1, entries[0].GetProperty("rank").GetInt32());
        Assert.Equal(_fx.OtherToday.Id, entries[0].GetProperty("submissionId").GetInt32());
        Assert.True(IsSigned(entries[0].GetProperty("screenshotUrl").GetString()!));
    }

    [Fact]
    public async Task League_leaderboard_for_a_past_day_is_visible_to_everyone()
    {
        var lb = await GetJsonAsync(Client(Viewer.NonSubmitter),
            $"/api/leagues/{_fx.League.Id}/leaderboard?gameId={_fx.Game.Id}&scoringDay={Day(HiddenScoresFixture.Yesterday)}");
        var entries = lb.GetProperty("entries").EnumerateArray().ToList();

        Assert.False(lb.GetProperty("scoresHidden").GetBoolean());
        Assert.Equal("80", entries[0].GetProperty("score").GetString());
        Assert.False(IsSigned(entries[0].GetProperty("screenshotUrl").GetString()!));
    }

    // ── GET /api/leagues/{id}/game-summaries ─────────────────────────────────

    [Fact]
    public async Task League_game_summaries_require_a_login()
    {
        var res = await Client(Viewer.Anonymous).GetAsync($"/api/leagues/{_fx.League.Id}/game-summaries");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Theory]
    [InlineData(Viewer.NonSubmitter, 80.0, null)]
    // The repository only knows the member, not the role: admins who did not submit are treated like members.
    [InlineData(Viewer.Admin, 80.0, null)]
    [InlineData(Viewer.Submitter, 100.0, 2)]
    public async Task League_game_summaries_leave_out_others_current_day_scores_for_non_submitters(Viewer viewer, double topScore, int? myRank)
    {
        var res = await GetJsonAsync(Client(viewer), $"/api/leagues/{_fx.League.Id}/game-summaries");
        var item = res.GetProperty("items").EnumerateArray().Single();

        Assert.Equal(topScore, item.GetProperty("topScoreValue").GetDouble());
        Assert.Equal(myRank, Has(item, "myRank") ? item.GetProperty("myRank").GetInt32() : null);
        // Play counts are not scores; they still include today.
        Assert.Equal(4, item.GetProperty("playCount").GetInt32());
    }
}
