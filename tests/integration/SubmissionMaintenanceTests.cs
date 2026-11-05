using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Services;
using DailyChallenges.TestUtilities;
using DailyChallenges.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DailyChallenges.IntegrationTests;

/// <summary>
/// Phase 6: admin delete / score edit with XP revocation and re-finalization, the score re-parse
/// maintenance endpoint and magic-byte upload validation. Clock: 2026-06-15 10:00 UTC; each test owns its database.
/// </summary>
public class SubmissionMaintenanceTests
{
    private static readonly DateTime Today = FixedClockApiFactory.Now.Date; // reset 00:00 → current day
    private static readonly DateTime Yesterday = Today.AddDays(-1);
    private const int WinXp = 100;
    private const int SubmissionXp = 50;

    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52 };

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static HttpClient Admin(FixedClockApiFactory f) => f.CreateAuthenticatedClient(99_000, "maint-admin", isAdmin: true);

    private static async Task<T> QueryAsync<T>(FixedClockApiFactory f, Func<AppDbContext, Task<T>> query)
    {
        T result = default!;
        await f.WithDbAsync(async db => result = await query(db));
        return result;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static Task<long> TotalXpAsync(FixedClockApiFactory f, int userId) =>
        QueryAsync(f, db => db.Users.Where(u => u.Id == userId).Select(u => u.TotalXp).SingleAsync());

    private static async Task<(int GameId, User A, User B)> SeedGameAndUsersAsync(FixedClockApiFactory f)
    {
        var game = new Game { Name = "Maint", ResetTime = TimeSpan.Zero, RankingMode = RankingMode.Highest };
        var a = new User { Username = "alice", PasswordHash = "x" };
        var b = new User { Username = "bob", PasswordHash = "x" };
        await f.WithDbAsync(async db => { db.AddRange(game, a, b); await db.SaveChangesAsync(); });
        return (game.Id, a, b);
    }

    private static MultipartFormDataContent SubmissionForm(int gameId, string score, byte[] file, string declaredType)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(gameId.ToString()), "gameId");
        form.Add(new StringContent(score), "score");
        var content = new ByteArrayContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue(declaredType);
        form.Add(content, "screenshot", "shot.bin");
        return form;
    }

    /// <summary>Seeds yesterday's submissions as if made through the API (XP awarded, "submission" event).</summary>
    private static async Task<Submission> SeedPastSubmissionAsync(FixedClockApiFactory f, int gameId, User user, string score, double? value)
    {
        var sub = new Submission
        {
            GameId = gameId,
            UserId = user.Id,
            Username = user.Username,
            Score = score,
            ScoreValue = value,
            CreatedAt = Yesterday.AddHours(8),
            ScoringDay = Yesterday,
            ScreenshotData = Png,
            ScreenshotContentType = "image/png",
            XpAwarded = SubmissionXp,
        };
        await f.WithDbAsync(async db =>
        {
            db.Submissions.Add(sub);
            await db.SaveChangesAsync();
            db.XpEvents.Add(new XpEvent { UserId = user.Id, SubmissionId = sub.Id, GameId = gameId, ScoringDay = Yesterday, Amount = SubmissionXp, EventType = "submission" });
            await db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.TotalXp, u => u.TotalXp + SubmissionXp));
            await db.SaveChangesAsync();
        });
        return sub;
    }

    private static async Task FinalizeAsync(FixedClockApiFactory f, int gameId, DateTime day)
    {
        using var scope = f.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IScoringDayFinalizerService>().FinalizeScoringDayAsync(gameId, day);
    }

    // ── Delete: XP revocation ────────────────────────────────────────────────

    [Fact]
    public async Task Delete_revokes_submission_xp_and_a_resubmission_is_paid_once_net()
    {
        using var f = new FixedClockApiFactory();
        var (gameId, alice, _) = await SeedGameAndUsersAsync(f);
        var client = f.CreateAuthenticatedClient(alice.Id, alice.Username);

        var created = await Json(await client.PostAsync("/api/submissions", SubmissionForm(gameId, "1 234", Png, "image/png")));
        Assert.Equal(SubmissionXp, created.GetProperty("xpGain").GetInt32());
        Assert.Equal(1234, created.GetProperty("submission").GetProperty("scoreValue").GetDouble());
        var subId = created.GetProperty("submission").GetProperty("id").GetInt32();
        Assert.Equal(SubmissionXp, await TotalXpAsync(f, alice.Id));

        var del = await Admin(f).DeleteAsync($"/api/submissions/{subId}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        Assert.Equal(0, await TotalXpAsync(f, alice.Id));
        var revoked = await QueryAsync(f, db => db.XpEvents.AsNoTracking().SingleAsync(e => e.UserId == alice.Id && e.EventType == "submission_revoked"));
        Assert.Equal(-SubmissionXp, revoked.Amount);
        Assert.Equal(gameId, revoked.GameId);
        Assert.Equal(Today, revoked.ScoringDay!.Value.Date);
        Assert.Contains($"submissionId={subId}", revoked.Details);
        Assert.False(await QueryAsync(f, db => db.Submissions.AnyAsync(s => s.Id == subId)));

        // The user may submit again for the same day; the balance ends at one submission's worth.
        var again = await Json(await client.PostAsync("/api/submissions", SubmissionForm(gameId, "2000", Png, "image/png")));
        Assert.Equal(SubmissionXp, again.GetProperty("xpGain").GetInt32());
        Assert.Equal(SubmissionXp, await TotalXpAsync(f, alice.Id));
        Assert.Equal(SubmissionXp, await QueryAsync(f, db => db.XpEvents.Where(e => e.UserId == alice.Id).SumAsync(e => e.Amount)));

        // "XP earned today" nets the revocation too.
        var dashboard = await Json(await client.GetAsync("/api/dashboard"));
        Assert.Equal(SubmissionXp, dashboard.GetProperty("xpEarnedToday").GetInt32());
    }

    [Fact]
    public async Task Deleting_a_missing_submission_is_a_no_op_and_non_admins_are_forbidden()
    {
        using var f = new FixedClockApiFactory();
        var (_, alice, _) = await SeedGameAndUsersAsync(f);

        Assert.Equal(HttpStatusCode.NoContent, (await Admin(f).DeleteAsync("/api/submissions/424242")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.CreateAuthenticatedClient(alice.Id, alice.Username).DeleteAsync("/api/submissions/1")).StatusCode);
    }

    // ── Delete / update on a finalized day: re-finalization ──────────────────

    [Fact]
    public async Task Deleting_the_finalized_winner_moves_the_win_to_the_runner_up()
    {
        using var f = new FixedClockApiFactory();
        var (gameId, alice, bob) = await SeedGameAndUsersAsync(f);
        var winning = await SeedPastSubmissionAsync(f, gameId, alice, "200", 200);
        await SeedPastSubmissionAsync(f, gameId, bob, "100", 100);
        await FinalizeAsync(f, gameId, Yesterday);
        Assert.Equal(SubmissionXp + WinXp, await TotalXpAsync(f, alice.Id));

        var del = await Admin(f).DeleteAsync($"/api/submissions/{winning.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // Alice: submission XP revoked, day_win event deleted and its XP removed.
        Assert.Equal(0, await TotalXpAsync(f, alice.Id));
        Assert.False(await QueryAsync(f, db => db.XpEvents.AnyAsync(e => e.UserId == alice.Id && e.EventType == "day_win")));
        Assert.True(await QueryAsync(f, db => db.XpEvents.AnyAsync(e => e.UserId == alice.Id && e.EventType == "submission_revoked" && e.Amount == -SubmissionXp)));

        // Bob: the new winner.
        var result = await QueryAsync(f, db => db.ScoringDayResults.AsNoTracking().SingleAsync(r => r.GameId == gameId));
        Assert.Equal(bob.Id, result.WinnerUserId);
        Assert.Equal(SubmissionXp + WinXp, await TotalXpAsync(f, bob.Id));
        Assert.Equal(1, await QueryAsync(f, db => db.XpEvents.CountAsync(e => e.EventType == "day_win")));

        // The day's result notifications were rewritten: only Bob's win remains.
        var notes = await QueryAsync(f, db => db.Notifications.AsNoTracking().Where(n => n.GameId == gameId).ToListAsync());
        var note = Assert.Single(notes);
        Assert.Equal(bob.Id, note.UserId);
        Assert.Equal("day_win", note.Type);
    }

    [Fact]
    public async Task Deleting_a_non_winner_on_a_finalized_day_keeps_the_result()
    {
        using var f = new FixedClockApiFactory();
        var (gameId, alice, bob) = await SeedGameAndUsersAsync(f);
        await SeedPastSubmissionAsync(f, gameId, alice, "200", 200);
        var losing = await SeedPastSubmissionAsync(f, gameId, bob, "100", 100);
        await FinalizeAsync(f, gameId, Yesterday);
        var resultId = await QueryAsync(f, db => db.ScoringDayResults.Select(r => r.Id).SingleAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await Admin(f).DeleteAsync($"/api/submissions/{losing.Id}")).StatusCode);

        Assert.Equal(resultId, await QueryAsync(f, db => db.ScoringDayResults.Select(r => r.Id).SingleAsync()));
        Assert.Equal(SubmissionXp + WinXp, await TotalXpAsync(f, alice.Id));
        Assert.Equal(0, await TotalXpAsync(f, bob.Id));
    }

    [Fact]
    public async Task Score_edit_that_changes_the_finalized_winner_refinalizes_the_day()
    {
        using var f = new FixedClockApiFactory();
        var (gameId, alice, bob) = await SeedGameAndUsersAsync(f);
        await SeedPastSubmissionAsync(f, gameId, alice, "200", 200);
        var bobs = await SeedPastSubmissionAsync(f, gameId, bob, "100", 100);
        await FinalizeAsync(f, gameId, Yesterday);
        var admin = Admin(f);

        // Still second: nothing is re-finalized.
        var resultId = await QueryAsync(f, db => db.ScoringDayResults.Select(r => r.Id).SingleAsync());
        await Json(await admin.PutAsJsonAsync($"/api/submissions/{bobs.Id}", new { score = "150" }));
        Assert.Equal(resultId, await QueryAsync(f, db => db.ScoringDayResults.Select(r => r.Id).SingleAsync()));
        Assert.Equal(SubmissionXp + WinXp, await TotalXpAsync(f, alice.Id));

        // Now first: the win moves to Bob.
        var dto = await Json(await admin.PutAsJsonAsync($"/api/submissions/{bobs.Id}", new { score = " 1,000 " }));
        Assert.Equal("1,000", dto.GetProperty("score").GetString());
        Assert.Equal(1000, dto.GetProperty("scoreValue").GetDouble());

        var result = await QueryAsync(f, db => db.ScoringDayResults.AsNoTracking().SingleAsync(r => r.GameId == gameId));
        Assert.Equal(bob.Id, result.WinnerUserId);
        Assert.Equal(SubmissionXp, await TotalXpAsync(f, alice.Id));
        Assert.Equal(SubmissionXp + WinXp, await TotalXpAsync(f, bob.Id));
        Assert.Equal(1, await QueryAsync(f, db => db.XpEvents.CountAsync(e => e.EventType == "day_win" && e.UserId == bob.Id)));
        Assert.False(await QueryAsync(f, db => db.XpEvents.AnyAsync(e => e.EventType == "day_win" && e.UserId == alice.Id)));
        // Fresh notifications: Bob won, Alice placed second.
        var notes = await QueryAsync(f, db => db.Notifications.AsNoTracking().Where(n => n.GameId == gameId).OrderBy(n => n.Rank).ToListAsync());
        Assert.Equal(new[] { (bob.Id, "day_win"), (alice.Id, "day_placement") }, notes.Select(n => (n.UserId, n.Type)));
    }

    [Fact]
    public async Task Score_edit_to_unparseable_text_makes_it_unranked_and_can_hand_over_the_win()
    {
        using var f = new FixedClockApiFactory();
        var (gameId, alice, bob) = await SeedGameAndUsersAsync(f);
        var alices = await SeedPastSubmissionAsync(f, gameId, alice, "200", 200);
        await SeedPastSubmissionAsync(f, gameId, bob, "100", 100);
        await FinalizeAsync(f, gameId, Yesterday);

        var dto = await Json(await Admin(f).PutAsJsonAsync($"/api/submissions/{alices.Id}", new { score = "DNF" }));
        Assert.False(dto.TryGetProperty("scoreValue", out var v) && v.ValueKind != JsonValueKind.Null);

        Assert.Equal(bob.Id, await QueryAsync(f, db => db.ScoringDayResults.Select(r => r.WinnerUserId).SingleAsync()));
        Assert.Equal(SubmissionXp, await TotalXpAsync(f, alice.Id));
    }

    // ── Re-parse maintenance ─────────────────────────────────────────────────

    [Fact]
    public async Task Reparse_scores_reports_on_dry_run_and_applies_on_request()
    {
        using var f = new FixedClockApiFactory();
        var (gameId, alice, bob) = await SeedGameAndUsersAsync(f);
        var stale = new (string Score, double? Value)[]
        {
            ("40.456", 40.456),   // old decimal reading → 40456
            ("3/6", 3),           // first-number parsing → unranked
            ("1 234", null),      // GLOB migration left it null → 1234
            ("2:05", 2),          // time → 125 seconds
            ("5", 5),             // unchanged
            ("abc", null),        // unchanged
        };
        var ids = new List<int>();
        await f.WithDbAsync(async db =>
        {
            for (int i = 0; i < stale.Length; i++)
            {
                var s = new Submission
                {
                    GameId = gameId, UserId = null, Username = $"anon{i}", Score = stale[i].Score, ScoreValue = stale[i].Value,
                    CreatedAt = Yesterday.AddHours(i), ScoringDay = Yesterday, ScreenshotData = Png, ScreenshotContentType = "image/png",
                };
                db.Submissions.Add(s);
                await db.SaveChangesAsync();
                ids.Add(s.Id);
            }
        });

        // Non-admins may not run it.
        var userClient = f.CreateAuthenticatedClient(alice.Id, alice.Username);
        Assert.Equal(HttpStatusCode.Forbidden, (await userClient.PostAsync("/api/admin/maintenance/reparse-scores?dryRun=true", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().PostAsync("/api/admin/maintenance/reparse-scores", null)).StatusCode);

        // Dry run (also the default): a report, no writes.
        var admin = Admin(f);
        var report = await Json(await admin.PostAsync("/api/admin/maintenance/reparse-scores", null));
        Assert.True(report.GetProperty("dryRun").GetBoolean());
        Assert.Equal(6, report.GetProperty("total").GetInt32());
        Assert.Equal(4, report.GetProperty("changed").GetInt32());
        Assert.Equal(1, report.GetProperty("becameNull").GetInt32());
        Assert.Equal(1, report.GetProperty("becameNumeric").GetInt32());
        Assert.Contains("not re-finalized", report.GetProperty("note").GetString());
        var samples = report.GetProperty("samples").EnumerateArray().ToList();
        Assert.Equal(4, samples.Count);
        var first = samples.Single(s => s.GetProperty("id").GetInt32() == ids[0]);
        Assert.Equal("40.456", first.GetProperty("score").GetString());
        Assert.Equal(40.456, first.GetProperty("oldValue").GetDouble());
        Assert.Equal(40456, first.GetProperty("newValue").GetDouble());
        var nulled = samples.Single(s => s.GetProperty("id").GetInt32() == ids[1]);
        Assert.False(nulled.TryGetProperty("newValue", out var nv) && nv.ValueKind != JsonValueKind.Null);

        var firstId = ids[0];
        Assert.Equal(40.456, await QueryAsync(f, db => db.Submissions.Where(s => s.Id == firstId).Select(s => s.ScoreValue).SingleAsync()));

        // Apply.
        var applied = await Json(await admin.PostAsync("/api/admin/maintenance/reparse-scores?dryRun=false", null));
        Assert.False(applied.GetProperty("dryRun").GetBoolean());
        Assert.Equal(4, applied.GetProperty("changed").GetInt32());

        var values = await QueryAsync(f, db => db.Submissions.OrderBy(s => s.Id).Select(s => s.ScoreValue).ToListAsync());
        Assert.Equal(new double?[] { 40456, null, 1234, 125, 5, null }, values);

        // Idempotent: nothing left to change.
        var again = await Json(await admin.PostAsync("/api/admin/maintenance/reparse-scores?dryRun=true", null));
        Assert.Equal(0, again.GetProperty("changed").GetInt32());
    }

    // ── Upload validation (magic bytes) ──────────────────────────────────────

    [Fact]
    public async Task Text_declared_as_png_is_rejected()
    {
        using var f = new FixedClockApiFactory();
        var (gameId, alice, _) = await SeedGameAndUsersAsync(f);

        var res = await f.CreateAuthenticatedClient(alice.Id, alice.Username)
            .PostAsync("/api/submissions", SubmissionForm(gameId, "5", "definitely not an image"u8.ToArray(), "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("invalid file type", await res.Content.ReadAsStringAsync());
        Assert.False(await QueryAsync(f, db => db.Submissions.AnyAsync()));
        Assert.Equal(0, await TotalXpAsync(f, alice.Id));
    }

    [Fact]
    public async Task Png_declared_as_jpeg_is_stored_and_served_as_png()
    {
        using var f = new FixedClockApiFactory();
        var (gameId, alice, _) = await SeedGameAndUsersAsync(f);

        var created = await Json(await f.CreateAuthenticatedClient(alice.Id, alice.Username)
            .PostAsync("/api/submissions", SubmissionForm(gameId, "5", Png, "image/jpeg")));
        var id = created.GetProperty("submission").GetProperty("id").GetInt32();

        Assert.Equal("image/png", await QueryAsync(f, db => db.Submissions.Where(s => s.Id == id).Select(s => s.ScreenshotContentType).SingleAsync()));

        var url = created.GetProperty("submission").GetProperty("screenshotUrl").GetString()!;
        var img = await f.CreateClient().GetAsync(url.StartsWith('/') ? url : "/" + url);
        Assert.Equal(HttpStatusCode.OK, img.StatusCode);
        Assert.Equal("image/png", img.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Game_image_type_comes_from_the_bytes()
    {
        using var f = new FixedClockApiFactory();
        var admin = Admin(f);

        var form = new MultipartFormDataContent { { new StringContent("Imaged"), "name" } };
        var image = new ByteArrayContent(Png);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/webp");
        form.Add(image, "image", "x.webp");
        var id = (await Json(await admin.PostAsync("/api/games", form))).GetProperty("id").GetInt32();
        Assert.Equal("image/png", await QueryAsync(f, db => db.Games.Where(g => g.Id == id).Select(g => g.ScreenshotContentType).SingleAsync()));

        var bad = new MultipartFormDataContent { { new StringContent("Fake"), "name" } };
        var text = new ByteArrayContent("<svg></svg>"u8.ToArray());
        text.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        bad.Add(text, "image", "x.png");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/games", bad)).StatusCode);
    }
    // ── Re-finalization failure after the commit ─────────────────────────────

    [Fact]
    public async Task A_failed_refinalization_after_delete_still_succeeds_and_the_background_job_retries_the_day()
    {
        using var f = new RefinalizeFailureApiFactory();
        var game = new Game { Name = "Maint", ResetTime = TimeSpan.Zero, RankingMode = RankingMode.Highest };
        var alice = new User { Username = "alice", PasswordHash = "x" };
        var bob = new User { Username = "bob", PasswordHash = "x" };
        await f.WithDbAsync(async db => { db.AddRange(game, alice, bob); await db.SaveChangesAsync(); });
        int winningId = 0;
        await f.WithDbAsync(async db =>
        {
            var a = new Submission { GameId = game.Id, UserId = alice.Id, Username = alice.Username, Score = "200", ScoreValue = 200, CreatedAt = Yesterday.AddHours(8), ScoringDay = Yesterday, ScreenshotData = Png, ScreenshotContentType = "image/png" };
            var b = new Submission { GameId = game.Id, UserId = bob.Id, Username = bob.Username, Score = "100", ScoreValue = 100, CreatedAt = Yesterday.AddHours(9), ScoringDay = Yesterday, ScreenshotData = Png, ScreenshotContentType = "image/png" };
            db.Submissions.AddRange(a, b);
            await db.SaveChangesAsync();
            winningId = a.Id;
        });
        using (var scope = f.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IScoringDayFinalizerService>().FinalizeScoringDayAsync(game.Id, Yesterday);
        Assert.True(await f.QueryAsync(db => db.ScoringDayResults.AnyAsync()));

        // The re-finalization after the delete throws; the delete itself is committed and reported as success.
        f.FailFinalization = true;
        var admin = f.CreateAuthenticatedClient(99_000, "maint-admin", isAdmin: true);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/submissions/{winningId}")).StatusCode);

        Assert.False(await f.QueryAsync(db => db.Submissions.AnyAsync(s => s.Id == winningId)));
        Assert.False(await f.QueryAsync(db => db.ScoringDayResults.AnyAsync()));
        Assert.False(await f.QueryAsync(db => db.XpEvents.AnyAsync(e => e.EventType == "day_win")));

        // The next background tick finalizes the day again, now with Bob as the winner.
        f.FailFinalization = false;
        await new ScoringDayBackgroundService(f.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<ScoringDayBackgroundService>.Instance, f.Clock)
            .ProcessAllGamesAsync();
        Assert.Equal(bob.Id, await f.QueryAsync(db => db.ScoringDayResults.Select(r => r.WinnerUserId).SingleAsync()));
    }

    // ── Admin unfiltered list ────────────────────────────────────────────────

    [Fact]
    public async Task Unfiltered_list_reports_the_stored_scoring_day_after_a_reset_time_change()
    {
        using var f = new FixedClockApiFactory();
        var (gameId, alice, _) = await SeedGameAndUsersAsync(f);
        await SeedPastSubmissionAsync(f, gameId, alice, "200", 200); // created yesterday 08:00, stored day = yesterday
        // With a 10:00 reset, 08:00 would now map to the day before yesterday; the stored day must win.
        await f.WithDbAsync(db => db.Games.Where(g => g.Id == gameId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.ResetTime, TimeSpan.FromHours(10))));

        var list = await Json(await Admin(f).GetAsync($"/api/submissions/game/{gameId}/unfiltered"));

        var item = Assert.Single(list.EnumerateArray().ToList());
        Assert.Equal(Yesterday.ToString("yyyy-MM-dd"), item.GetProperty("scoringDay").GetString());
    }
}

/// <summary>Fixed clock (2026-06-15 10:00 UTC) plus a switch that makes every finalizer call throw.</summary>
public sealed class RefinalizeFailureApiFactory : ApiFactory
{
    public FixedTimeProvider Clock { get; } = new(FixedClockApiFactory.Now);

    public volatile bool FailFinalization;

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(Clock);
        services.RemoveAll<IScoringDayFinalizerService>();
        services.AddScoped<IScoringDayFinalizerService>(sp => FailFinalization
            ? new ThrowingFinalizer()
            : ActivatorUtilities.CreateInstance<ScoringDayFinalizerService>(sp));
    }

    public async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        T result = default!;
        await WithDbAsync(async db => result = await query(db));
        return result;
    }

    private sealed class ThrowingFinalizer : IScoringDayFinalizerService
    {
        public Task FinalizeScoringDayAsync(int gameId, DateTime scoringDay) => throw new InvalidOperationException("finalizer down");
    }
}
