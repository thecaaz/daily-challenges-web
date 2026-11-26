using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DailyChallenges.IntegrationTests.Infrastructure;
using DailyChallenges.Models;
using DailyChallenges.Services;
using DailyChallenges.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using static DailyChallenges.IntegrationTests.Infrastructure.HttpResponseJson;

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

    private static HttpClient Admin(ApiFactory f) => f.CreateAuthenticatedClient(99_000, "maint-admin", isAdmin: true);

    private static Task<long> TotalXpAsync(ApiFactory f, int userId) =>
        f.QueryDbAsync(db => db.Users.Where(u => u.Id == userId).Select(u => u.TotalXp).SingleAsync());

    private static async Task<(int GameId, User A, User B)> SeedGameAndUsersAsync(ApiFactory f)
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
    private static async Task<Submission> SeedPastSubmissionAsync(ApiFactory f, int gameId, User user, string score, double? value)
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

    // ── Delete: XP revocation ────────────────────────────────────────────────

    /// <summary>Submits "1 234" for today through <paramref name="client"/>, then deletes it as an admin; returns the deleted id.</summary>
    private static async Task<int> SubmitTodayAndDeleteAsync(FixedClockApiFactory f, HttpClient client, int gameId)
    {
        var created = await ReadSuccessJsonAsync(await client.PostAsync("/api/submissions", SubmissionForm(gameId, "1 234", Png, "image/png")));
        var submissionId = created.GetProperty("submission").GetProperty("id").GetInt32();
        var deleted = await Admin(f).DeleteAsync($"/api/submissions/{submissionId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        return submissionId;
    }

    [Fact]
    public async Task Submit_awards_submission_xp_and_parses_the_score()
    {
        using var f = new FixedClockApiFactory();
        var (gameId, alice, _) = await SeedGameAndUsersAsync(f);
        var client = f.CreateAuthenticatedClient(alice.Id, alice.Username);

        var created = await ReadSuccessJsonAsync(await client.PostAsync("/api/submissions", SubmissionForm(gameId, "1 234", Png, "image/png")));

        Assert.Equal(SubmissionXp, created.GetProperty("xpGain").GetInt32());
        Assert.Equal(1234, created.GetProperty("submission").GetProperty("scoreValue").GetDouble());
        Assert.Equal(SubmissionXp, await TotalXpAsync(f, alice.Id));
    }

    [Fact]
    public async Task Delete_revokes_the_submission_xp_with_a_revocation_event()
    {
        using var f = new FixedClockApiFactory();
        var (gameId, alice, _) = await SeedGameAndUsersAsync(f);

        var subId = await SubmitTodayAndDeleteAsync(f, f.CreateAuthenticatedClient(alice.Id, alice.Username), gameId);

        Assert.Equal(0, await TotalXpAsync(f, alice.Id));
        var revoked = await f.QueryDbAsync(db => db.XpEvents.AsNoTracking().SingleAsync(e => e.UserId == alice.Id && e.EventType == "submission_revoked"));
        Assert.Equal(-SubmissionXp, revoked.Amount);
        Assert.Equal(gameId, revoked.GameId);
        Assert.Equal(Today, revoked.ScoringDay!.Value.Date);
        Assert.Contains($"submissionId={subId}", revoked.Details);
        Assert.False(await f.QueryDbAsync(db => db.Submissions.AnyAsync(s => s.Id == subId)));
    }

    [Fact]
    public async Task Resubmitting_after_a_delete_pays_one_submission_worth_of_xp_net()
    {
        using var f = new FixedClockApiFactory();
        var (gameId, alice, _) = await SeedGameAndUsersAsync(f);
        var client = f.CreateAuthenticatedClient(alice.Id, alice.Username);
        await SubmitTodayAndDeleteAsync(f, client, gameId);

        var again = await ReadSuccessJsonAsync(await client.PostAsync("/api/submissions", SubmissionForm(gameId, "2000", Png, "image/png")));

        Assert.Equal(SubmissionXp, again.GetProperty("xpGain").GetInt32());
        Assert.Equal(SubmissionXp, await TotalXpAsync(f, alice.Id));
        Assert.Equal(SubmissionXp, await f.QueryDbAsync(db => db.XpEvents.Where(e => e.UserId == alice.Id).SumAsync(e => e.Amount)));
    }

    [Fact]
    public async Task Dashboard_xp_earned_today_nets_a_revoked_submission()
    {
        using var f = new FixedClockApiFactory();
        var (gameId, alice, _) = await SeedGameAndUsersAsync(f);
        var client = f.CreateAuthenticatedClient(alice.Id, alice.Username);
        await SubmitTodayAndDeleteAsync(f, client, gameId);
        await ReadSuccessJsonAsync(await client.PostAsync("/api/submissions", SubmissionForm(gameId, "2000", Png, "image/png")));

        var dashboard = await ReadSuccessJsonAsync(await client.GetAsync("/api/dashboard"));

        Assert.Equal(SubmissionXp, dashboard.GetProperty("xpEarnedToday").GetInt32());
    }

    [Fact]
    public async Task Deleting_a_missing_submission_is_a_no_op()
    {
        using var f = new FixedClockApiFactory();
        const int missingSubmissionId = 424242;

        var res = await Admin(f).DeleteAsync($"/api/submissions/{missingSubmissionId}");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_submission_as_a_non_admin_is_forbidden()
    {
        using var f = new FixedClockApiFactory();
        var (_, alice, _) = await SeedGameAndUsersAsync(f);

        var res = await f.CreateAuthenticatedClient(alice.Id, alice.Username).DeleteAsync("/api/submissions/1");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ── Delete / update on a finalized day: re-finalization ──────────────────

    /// <summary>Alice (200) beats Bob (100) yesterday, the day is finalized, then an admin deletes Alice's submission.</summary>
    private static async Task<(int GameId, User Alice, User Bob)> DeleteFinalizedWinnerAsync(FixedClockApiFactory f)
    {
        var (gameId, alice, bob) = await SeedGameAndUsersAsync(f);
        var winning = await SeedPastSubmissionAsync(f, gameId, alice, "200", 200);
        await SeedPastSubmissionAsync(f, gameId, bob, "100", 100);
        await f.FinalizeScoringDayAsync(gameId, Yesterday);
        Assert.Equal(SubmissionXp + WinXp, await TotalXpAsync(f, alice.Id));

        var del = await Admin(f).DeleteAsync($"/api/submissions/{winning.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
        return (gameId, alice, bob);
    }

    [Fact]
    public async Task Deleting_the_finalized_winner_revokes_their_submission_and_win_xp()
    {
        using var f = new FixedClockApiFactory();

        var (_, alice, _) = await DeleteFinalizedWinnerAsync(f);

        Assert.Equal(0, await TotalXpAsync(f, alice.Id));
        Assert.False(await f.QueryDbAsync(db => db.XpEvents.AnyAsync(e => e.UserId == alice.Id && e.EventType == "day_win")));
        Assert.True(await f.QueryDbAsync(db => db.XpEvents.AnyAsync(e => e.UserId == alice.Id && e.EventType == "submission_revoked" && e.Amount == -SubmissionXp)));
    }

    [Fact]
    public async Task Deleting_the_finalized_winner_moves_the_win_to_the_runner_up()
    {
        using var f = new FixedClockApiFactory();

        var (gameId, _, bob) = await DeleteFinalizedWinnerAsync(f);

        var result = await f.QueryDbAsync(db => db.ScoringDayResults.AsNoTracking().SingleAsync(r => r.GameId == gameId));
        Assert.Equal(bob.Id, result.WinnerUserId);
        Assert.Equal(SubmissionXp + WinXp, await TotalXpAsync(f, bob.Id));
        Assert.Equal(1, await f.QueryDbAsync(db => db.XpEvents.CountAsync(e => e.EventType == "day_win")));
    }

    [Fact]
    public async Task Deleting_the_finalized_winner_leaves_only_the_new_winners_notification()
    {
        using var f = new FixedClockApiFactory();

        var (gameId, _, bob) = await DeleteFinalizedWinnerAsync(f);

        var notes = await f.QueryDbAsync(db => db.Notifications.AsNoTracking().Where(n => n.GameId == gameId).ToListAsync());
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
        await f.FinalizeScoringDayAsync(gameId, Yesterday);
        var resultId = await f.QueryDbAsync(db => db.ScoringDayResults.Select(r => r.Id).SingleAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await Admin(f).DeleteAsync($"/api/submissions/{losing.Id}")).StatusCode);

        Assert.Equal(resultId, await f.QueryDbAsync(db => db.ScoringDayResults.Select(r => r.Id).SingleAsync()));
        Assert.Equal(SubmissionXp + WinXp, await TotalXpAsync(f, alice.Id));
        Assert.Equal(0, await TotalXpAsync(f, bob.Id));
    }

    /// <summary>Alice (200) beats Bob (100) yesterday and the day is finalized; returns Bob's submission.</summary>
    private static async Task<(int GameId, User Alice, User Bob, Submission BobsSubmission)> SeedFinalizedDayWithBobSecondAsync(FixedClockApiFactory f)
    {
        var (gameId, alice, bob) = await SeedGameAndUsersAsync(f);
        await SeedPastSubmissionAsync(f, gameId, alice, "200", 200);
        var bobs = await SeedPastSubmissionAsync(f, gameId, bob, "100", 100);
        await f.FinalizeScoringDayAsync(gameId, Yesterday);
        return (gameId, alice, bob, bobs);
    }

    [Fact]
    public async Task Score_edit_that_keeps_the_finalized_winner_does_not_refinalize_the_day()
    {
        using var f = new FixedClockApiFactory();
        var (_, alice, _, bobs) = await SeedFinalizedDayWithBobSecondAsync(f);
        var resultId = await f.QueryDbAsync(db => db.ScoringDayResults.Select(r => r.Id).SingleAsync());

        await ReadSuccessJsonAsync(await Admin(f).PutAsJsonAsync($"/api/submissions/{bobs.Id}", new { score = "150" }));

        Assert.Equal(resultId, await f.QueryDbAsync(db => db.ScoringDayResults.Select(r => r.Id).SingleAsync()));
        Assert.Equal(SubmissionXp + WinXp, await TotalXpAsync(f, alice.Id));
    }

    [Fact]
    public async Task Score_edit_that_changes_the_finalized_winner_refinalizes_the_day()
    {
        using var f = new FixedClockApiFactory();
        var (gameId, alice, bob, bobs) = await SeedFinalizedDayWithBobSecondAsync(f);

        var dto = await ReadSuccessJsonAsync(await Admin(f).PutAsJsonAsync($"/api/submissions/{bobs.Id}", new { score = " 1,000 " }));

        Assert.Equal("1,000", dto.GetProperty("score").GetString());
        Assert.Equal(1000, dto.GetProperty("scoreValue").GetDouble());
        var result = await f.QueryDbAsync(db => db.ScoringDayResults.AsNoTracking().SingleAsync(r => r.GameId == gameId));
        Assert.Equal(bob.Id, result.WinnerUserId);
        Assert.Equal(SubmissionXp, await TotalXpAsync(f, alice.Id));
        Assert.Equal(SubmissionXp + WinXp, await TotalXpAsync(f, bob.Id));
        Assert.Equal(1, await f.QueryDbAsync(db => db.XpEvents.CountAsync(e => e.EventType == "day_win" && e.UserId == bob.Id)));
        Assert.False(await f.QueryDbAsync(db => db.XpEvents.AnyAsync(e => e.EventType == "day_win" && e.UserId == alice.Id)));
    }

    [Fact]
    public async Task Score_edit_that_changes_the_finalized_winner_rewrites_the_result_notifications()
    {
        using var f = new FixedClockApiFactory();
        var (gameId, alice, bob, bobs) = await SeedFinalizedDayWithBobSecondAsync(f);

        await ReadSuccessJsonAsync(await Admin(f).PutAsJsonAsync($"/api/submissions/{bobs.Id}", new { score = " 1,000 " }));

        var notes = await f.QueryDbAsync(db => db.Notifications.AsNoTracking().Where(n => n.GameId == gameId).OrderBy(n => n.Rank).ToListAsync());
        Assert.Equal(new[] { (bob.Id, "day_win"), (alice.Id, "day_placement") }, notes.Select(n => (n.UserId, n.Type)));
    }

    [Fact]
    public async Task Score_edit_to_unparseable_text_makes_it_unranked_and_can_hand_over_the_win()
    {
        using var f = new FixedClockApiFactory();
        var (gameId, alice, bob) = await SeedGameAndUsersAsync(f);
        var alices = await SeedPastSubmissionAsync(f, gameId, alice, "200", 200);
        await SeedPastSubmissionAsync(f, gameId, bob, "100", 100);
        await f.FinalizeScoringDayAsync(gameId, Yesterday);

        var dto = await ReadSuccessJsonAsync(await Admin(f).PutAsJsonAsync($"/api/submissions/{alices.Id}", new { score = "DNF" }));
        Assert.False(dto.TryGetProperty("scoreValue", out var v) && v.ValueKind != JsonValueKind.Null);

        Assert.Equal(bob.Id, await f.QueryDbAsync(db => db.ScoringDayResults.Select(r => r.WinnerUserId).SingleAsync()));
        Assert.Equal(SubmissionXp, await TotalXpAsync(f, alice.Id));
    }

    // ── Re-parse maintenance ─────────────────────────────────────────────────

    private const string ReparseUrl = "/api/admin/maintenance/reparse-scores";

    /// <summary>
    /// Scores with values stored by older parsers, in insertion order: "40.456" was read as a decimal (now 40456),
    /// "3/6" by first-number parsing (now unranked), "1 234" left null by the GLOB migration (now 1234),
    /// "2:05" as 2 (now 125 seconds); "5" and "abc" are already right. See <see cref="ReparsedValues"/>.
    /// </summary>
    private static readonly (string Score, double? StoredValue)[] StaleScores =
    {
        ("40.456", 40.456),
        ("3/6", 3),
        ("1 234", null),
        ("2:05", 2),
        ("5", 5),
        ("abc", null),
    };

    private static readonly double?[] ReparsedValues = { 40456, null, 1234, 125, 5, null };

    /// <summary>Seeds <see cref="StaleScores"/> as anonymous submissions for yesterday; returns their ids in order.</summary>
    private static async Task<List<int>> SeedStaleScoresAsync(FixedClockApiFactory f, int gameId)
    {
        var ids = new List<int>();
        await f.WithDbAsync(async db =>
        {
            for (int i = 0; i < StaleScores.Length; i++)
            {
                var s = new Submission
                {
                    GameId = gameId, UserId = null, Username = $"anon{i}", Score = StaleScores[i].Score, ScoreValue = StaleScores[i].StoredValue,
                    CreatedAt = Yesterday.AddHours(i), ScoringDay = Yesterday, ScreenshotData = Png, ScreenshotContentType = "image/png",
                };
                db.Submissions.Add(s);
                await db.SaveChangesAsync();
                ids.Add(s.Id);
            }
        });
        return ids;
    }

    [Fact]
    public async Task Reparse_scores_is_forbidden_for_non_admins()
    {
        using var f = new FixedClockApiFactory();
        var (_, alice, _) = await SeedGameAndUsersAsync(f);

        var res = await f.CreateAuthenticatedClient(alice.Id, alice.Username).PostAsync($"{ReparseUrl}?dryRun=true", null);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Reparse_scores_requires_a_login()
    {
        using var f = new FixedClockApiFactory();

        var res = await f.CreateClient().PostAsync(ReparseUrl, null);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Reparse_scores_defaults_to_a_dry_run_that_reports_the_changes()
    {
        using var f = new FixedClockApiFactory();
        var (gameId, _, _) = await SeedGameAndUsersAsync(f);
        var ids = await SeedStaleScoresAsync(f, gameId);

        var report = await ReadSuccessJsonAsync(await Admin(f).PostAsync(ReparseUrl, null));

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
    }

    [Fact]
    public async Task Reparse_scores_dry_run_writes_nothing()
    {
        using var f = new FixedClockApiFactory();
        var (gameId, _, _) = await SeedGameAndUsersAsync(f);
        var firstId = (await SeedStaleScoresAsync(f, gameId))[0];

        await ReadSuccessJsonAsync(await Admin(f).PostAsync(ReparseUrl, null));

        Assert.Equal(40.456, await f.QueryDbAsync(db => db.Submissions.Where(s => s.Id == firstId).Select(s => s.ScoreValue).SingleAsync()));
    }

    [Fact]
    public async Task Reparse_scores_without_dry_run_rewrites_the_stale_values()
    {
        using var f = new FixedClockApiFactory();
        var (gameId, _, _) = await SeedGameAndUsersAsync(f);
        await SeedStaleScoresAsync(f, gameId);

        var applied = await ReadSuccessJsonAsync(await Admin(f).PostAsync($"{ReparseUrl}?dryRun=false", null));

        Assert.False(applied.GetProperty("dryRun").GetBoolean());
        Assert.Equal(4, applied.GetProperty("changed").GetInt32());
        var values = await f.QueryDbAsync(db => db.Submissions.OrderBy(s => s.Id).Select(s => s.ScoreValue).ToListAsync());
        Assert.Equal(ReparsedValues, values);
    }

    [Fact]
    public async Task Reparse_scores_after_applying_finds_nothing_left_to_change()
    {
        using var f = new FixedClockApiFactory();
        var (gameId, _, _) = await SeedGameAndUsersAsync(f);
        await SeedStaleScoresAsync(f, gameId);
        var admin = Admin(f);
        await ReadSuccessJsonAsync(await admin.PostAsync($"{ReparseUrl}?dryRun=false", null));

        var again = await ReadSuccessJsonAsync(await admin.PostAsync($"{ReparseUrl}?dryRun=true", null));

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
        Assert.False(await f.QueryDbAsync(db => db.Submissions.AnyAsync()));
        Assert.Equal(0, await TotalXpAsync(f, alice.Id));
    }

    [Fact]
    public async Task Png_declared_as_jpeg_is_stored_and_served_as_png()
    {
        using var f = new FixedClockApiFactory();
        var (gameId, alice, _) = await SeedGameAndUsersAsync(f);

        var created = await ReadSuccessJsonAsync(await f.CreateAuthenticatedClient(alice.Id, alice.Username)
            .PostAsync("/api/submissions", SubmissionForm(gameId, "5", Png, "image/jpeg")));
        var id = created.GetProperty("submission").GetProperty("id").GetInt32();

        Assert.Equal("image/png", await f.QueryDbAsync(db => db.Submissions.Where(s => s.Id == id).Select(s => s.ScreenshotContentType).SingleAsync()));

        var url = created.GetProperty("submission").GetProperty("screenshotUrl").GetString()!;
        var img = await f.CreateClient().GetAsync(url.StartsWith('/') ? url : "/" + url);
        Assert.Equal(HttpStatusCode.OK, img.StatusCode);
        Assert.Equal("image/png", img.Content.Headers.ContentType?.MediaType);
    }

    private static MultipartFormDataContent GameForm(string name, byte[] image, string declaredType, string fileName)
    {
        var form = new MultipartFormDataContent { { new StringContent(name), "name" } };
        var content = new ByteArrayContent(image);
        content.Headers.ContentType = new MediaTypeHeaderValue(declaredType);
        form.Add(content, "image", fileName);
        return form;
    }

    [Fact]
    public async Task Game_image_type_comes_from_the_bytes()
    {
        using var f = new FixedClockApiFactory();

        var created = await ReadSuccessJsonAsync(await Admin(f).PostAsync("/api/games", GameForm("Imaged", Png, "image/webp", "x.webp")));

        var id = created.GetProperty("id").GetInt32();
        Assert.Equal("image/png", await f.QueryDbAsync(db => db.Games.Where(g => g.Id == id).Select(g => g.ScreenshotContentType).SingleAsync()));
    }

    [Fact]
    public async Task Game_image_that_is_not_an_image_is_rejected()
    {
        using var f = new FixedClockApiFactory();

        var res = await Admin(f).PostAsync("/api/games", GameForm("Fake", "<svg></svg>"u8.ToArray(), "image/png", "x.png"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ── Re-finalization failure after the commit ─────────────────────────────

    /// <summary>
    /// Alice (200) beats Bob (100) yesterday and the day is finalized; then the finalizer starts failing and an admin
    /// deletes Alice's submission. Returns the deleted id and Bob.
    /// </summary>
    private static async Task<(int DeletedId, User Bob)> DeleteWinnerWhileRefinalizationFailsAsync(RefinalizeFailureApiFactory f)
    {
        var (gameId, alice, bob) = await SeedGameAndUsersAsync(f);
        var winning = await SeedPastSubmissionAsync(f, gameId, alice, "200", 200);
        await SeedPastSubmissionAsync(f, gameId, bob, "100", 100);
        await f.FinalizeScoringDayAsync(gameId, Yesterday);
        Assert.True(await f.QueryDbAsync(db => db.ScoringDayResults.AnyAsync()));

        f.FailFinalization = true;
        var res = await Admin(f).DeleteAsync($"/api/submissions/{winning.Id}");
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        return (winning.Id, bob);
    }

    [Fact]
    public async Task A_failed_refinalization_after_delete_still_commits_the_delete()
    {
        using var f = new RefinalizeFailureApiFactory();

        var (deletedId, _) = await DeleteWinnerWhileRefinalizationFailsAsync(f);

        Assert.False(await f.QueryDbAsync(db => db.Submissions.AnyAsync(s => s.Id == deletedId)));
        Assert.False(await f.QueryDbAsync(db => db.ScoringDayResults.AnyAsync()));
        Assert.False(await f.QueryDbAsync(db => db.XpEvents.AnyAsync(e => e.EventType == "day_win")));
    }

    [Fact]
    public async Task Background_job_refinalizes_a_day_whose_refinalization_failed()
    {
        using var f = new RefinalizeFailureApiFactory();
        var (_, bob) = await DeleteWinnerWhileRefinalizationFailsAsync(f);
        f.FailFinalization = false;

        await new ScoringDayBackgroundService(f.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<ScoringDayBackgroundService>.Instance, f.Clock)
            .ProcessAllGamesAsync(CancellationToken.None);

        Assert.Equal(bob.Id, await f.QueryDbAsync(db => db.ScoringDayResults.Select(r => r.WinnerUserId).SingleAsync()));
    }

    // ── Admin unfiltered list ────────────────────────────────────────────────

    /// <summary>
    /// The submission was created yesterday 08:00 with stored day = yesterday. With a 10:00 reset, 08:00 would now map
    /// to the day before yesterday; the stored day must win.
    /// </summary>
    [Fact]
    public async Task Unfiltered_list_reports_the_stored_scoring_day_after_a_reset_time_change()
    {
        using var f = new FixedClockApiFactory();
        var (gameId, alice, _) = await SeedGameAndUsersAsync(f);
        await SeedPastSubmissionAsync(f, gameId, alice, "200", 200);
        var resetLaterThanTheSubmission = TimeSpan.FromHours(10);
        await f.WithDbAsync(db => db.Games.Where(g => g.Id == gameId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.ResetTime, resetLaterThanTheSubmission)));

        var list = await ReadSuccessJsonAsync(await Admin(f).GetAsync($"/api/submissions/game/{gameId}/unfiltered"));

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

    private sealed class ThrowingFinalizer : IScoringDayFinalizerService
    {
        public Task FinalizeScoringDayAsync(int gameId, DateTime scoringDay, CancellationToken ct) => throw new InvalidOperationException("finalizer down");
    }
}
