using System.Net;
using System.Text.Json;
using DailyChallenges.IntegrationTests.Infrastructure;
using DailyChallenges.Models;
using static DailyChallenges.IntegrationTests.Infrastructure.HttpResponseJson;

namespace DailyChallenges.IntegrationTests;

/// <summary>
/// Game read paths over HTTP: the per-user flags of the game list, the overview's include filter and
/// favorite flag, personal highscore ranks and not-found responses. Clock: 2026-06-15 10:00 UTC.
/// </summary>
public class GameReadApiTests : IClassFixture<FixedClockApiFactory>
{
    private static readonly DateTime Today = FixedClockApiFactory.Now.Date;

    private readonly FixedClockApiFactory _factory;

    public GameReadApiTests(FixedClockApiFactory factory) => _factory = factory;

    private sealed record Seed(User Player, Game Played, Game Favorite);

    private async Task<Seed> SeedAsync(string prefix)
    {
        var player = new User { Username = $"{prefix}-player", PasswordHash = "x" };
        var played = new Game { Name = $"{prefix}-played", ResetTime = TimeSpan.Zero, RankingMode = RankingMode.Highest };
        var favorite = new Game { Name = $"{prefix}-favorite", ResetTime = TimeSpan.Zero, RankingMode = RankingMode.Highest };
        await _factory.WithDbAsync(async db =>
        {
            db.Users.Add(player);
            db.Games.AddRange(played, favorite);
            await db.SaveChangesAsync();

            db.Submissions.AddRange(
                Submission(played, player, "30", 30, Today.AddHours(8)),
                Submission(played, player, "90", 90, Today.AddDays(-1).AddHours(8)),
                Submission(played, player, "n/a", null, Today.AddDays(-2).AddHours(8)),
                Submission(favorite, player, "10", 10, Today.AddDays(-3).AddHours(8)));
            db.Favorites.Add(new Favorite { UserId = player.Id, GameId = favorite.Id });
            await db.SaveChangesAsync();
        });
        return new Seed(player, played, favorite);
    }

    private static Submission Submission(Game game, User user, string score, double? value, DateTime createdAt) => new()
    {
        GameId = game.Id,
        UserId = user.Id,
        Username = user.Username,
        Score = score,
        ScoreValue = value,
        CreatedAt = createdAt,
        ScoringDay = createdAt.Date,
    };

    private HttpClient As(User user) => _factory.CreateAuthenticatedClient(user.Id, user.Username);

    private static JsonElement GameNamed(JsonElement list, string name) =>
        list.EnumerateArray().Single(g => g.GetProperty("name").GetString() == name);

    [Fact]
    public async Task Game_list_flags_todays_submission_and_favorites_for_the_signed_in_user()
    {
        var seed = await SeedAsync("list");

        var list = await ReadSuccessJsonAsync(await As(seed.Player).GetAsync("/api/games"));

        var played = GameNamed(list, seed.Played.Name);
        var favorite = GameNamed(list, seed.Favorite.Name);
        Assert.True(played.GetProperty("hasSubmittedForLatest").GetBoolean());
        Assert.False(played.GetProperty("isFavorite").GetBoolean());
        Assert.False(favorite.GetProperty("hasSubmittedForLatest").GetBoolean());
        Assert.True(favorite.GetProperty("isFavorite").GetBoolean());
    }

    [Fact]
    public async Task Game_list_for_anonymous_callers_has_no_user_flags()
    {
        var seed = await SeedAsync("anon-list");

        var list = await ReadSuccessJsonAsync(await _factory.CreateClient().GetAsync("/api/games"));

        var played = GameNamed(list, seed.Played.Name);
        Assert.False(played.GetProperty("hasSubmittedForLatest").GetBoolean());
        Assert.False(GameNamed(list, seed.Favorite.Name).GetProperty("isFavorite").GetBoolean());
    }

    [Fact]
    public async Task Overview_without_include_returns_every_part_and_no_errors()
    {
        var seed = await SeedAsync("overview-all");

        var overview = await ReadSuccessJsonAsync(await As(seed.Player).GetAsync($"/api/games/{seed.Played.Id}/overview"));

        var dates = overview.GetProperty("availableDates").EnumerateArray().Select(d => d.GetString()).ToList();
        Assert.Equal(new[] { "2026-06-15", "2026-06-14", "2026-06-13" }, dates);
        Assert.True(overview.GetProperty("hasSubmittedForLatest").GetBoolean());
        Assert.False(overview.TryGetProperty("errors", out _));
        Assert.Equal(seed.Played.Id, overview.GetProperty("game").GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Overview_include_filter_leaves_out_the_parts_not_asked_for()
    {
        var seed = await SeedAsync("overview-include");
        var client = As(seed.Player);

        var datesOnly = await ReadSuccessJsonAsync(await client.GetAsync($"/api/games/{seed.Played.Id}/overview?include=availableDates"));
        var submittedOnly = await ReadSuccessJsonAsync(await client.GetAsync($"/api/games/{seed.Played.Id}/overview?include= hasSubmitted ,"));

        Assert.Equal(3, datesOnly.GetProperty("availableDates").GetArrayLength());
        Assert.False(datesOnly.GetProperty("hasSubmittedForLatest").GetBoolean());
        Assert.Equal(0, submittedOnly.GetProperty("availableDates").GetArrayLength());
        Assert.True(submittedOnly.GetProperty("hasSubmittedForLatest").GetBoolean());
    }

    [Fact]
    public async Task Overview_and_game_detail_mark_the_callers_favorite()
    {
        var seed = await SeedAsync("overview-fav");
        var client = As(seed.Player);

        var overview = await ReadSuccessJsonAsync(await client.GetAsync($"/api/games/{seed.Favorite.Id}/overview"));
        var detail = await ReadSuccessJsonAsync(await client.GetAsync($"/api/games/{seed.Favorite.Id}"));
        var anonymous = await ReadSuccessJsonAsync(await _factory.CreateClient().GetAsync($"/api/games/{seed.Favorite.Id}"));

        Assert.True(overview.GetProperty("game").GetProperty("isFavorite").GetBoolean());
        Assert.True(detail.GetProperty("isFavorite").GetBoolean());
        Assert.False(anonymous.GetProperty("isFavorite").GetBoolean());
    }

    [Fact]
    public async Task Personal_highscore_ranks_scored_submissions_best_first_and_leaves_out_unscored_ones()
    {
        var seed = await SeedAsync("personal");

        var result = await ReadSuccessJsonAsync(await As(seed.Player).GetAsync($"/api/games/{seed.Played.Id}/personal-highscore"));

        var top = result.GetProperty("top").EnumerateArray().ToList();
        Assert.Equal(new[] { "90", "30" }, top.Select(s => s.GetProperty("score").GetString()));
        Assert.Equal(new[] { 1, 2 }, top.Select(s => s.GetProperty("rank").GetInt32()));
        Assert.Equal("90", result.GetProperty("personalHighscore").GetProperty("score").GetString());
    }

    [Fact]
    public async Task Missing_game_returns_404_on_read_and_update_paths()
    {
        var admin = _factory.CreateAuthenticatedClient(1, "read-admin", isAdmin: true);
        var form = new MultipartFormDataContent { { new StringContent("renamed"), "name" } };

        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync("/api/games/987650")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync("/api/games/987650/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync("/api/games/987650/highscore")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsync("/api/games/987650", form)).StatusCode);
    }
}
