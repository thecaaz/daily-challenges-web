using System.Net;
using System.Text.Json;
using DailyChallenges.Achievements;
using DailyChallenges.IntegrationTests.Infrastructure;
using DailyChallenges.Models;

namespace DailyChallenges.IntegrationTests;

public class AchievementsApiTests : IClassFixture<ApiFactory>
{
    private static readonly DateTime UnlockedAt = new(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);

    private readonly ApiFactory _factory;

    public AchievementsApiTests(ApiFactory factory) => _factory = factory;

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {body}");
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    private async Task<int> SeedUserWithWin1Async(string username)
    {
        var user = new User { Username = username, PasswordHash = "x" };
        await _factory.WithDbAsync(async db =>
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
            db.UserAchievements.Add(new UserAchievement { UserId = user.Id, AchievementId = "win_1", UnlockedAt = UnlockedAt });
            await db.SaveChangesAsync();
        });
        return user.Id;
    }

    [Fact]
    public async Task Catalog_lists_every_achievement_with_exactly_its_public_fields()
    {
        var catalog = (await ReadJsonAsync(await _factory.CreateClient().GetAsync("/api/achievements"))).EnumerateArray().ToList();

        Assert.Equal(AchievementCatalog.All.Select(a => a.Id), catalog.Select(c => c.GetProperty("achievementId").GetString()));
        Assert.All(catalog, c => Assert.Equal(
            new[] { "achievementId", "name", "description", "iconKey" },
            c.EnumerateObject().Select(p => p.Name)));
        Assert.Equal("Winner", catalog.Single(c => c.GetProperty("achievementId").GetString() == "win_1").GetProperty("name").GetString());
    }

    [Fact]
    public async Task User_achievements_merge_the_unlock_state_into_the_catalog()
    {
        var userId = await SeedUserWithWin1Async("ach-public");

        var list = (await ReadJsonAsync(await _factory.CreateClient().GetAsync($"/api/users/{userId}/achievements"))).EnumerateArray().ToList();

        Assert.Equal(AchievementCatalog.All.Count, list.Count);
        var win1 = list.Single(a => a.GetProperty("achievementId").GetString() == "win_1");
        Assert.Equal(UnlockedAt, win1.GetProperty("unlockedAt").GetDateTime().ToUniversalTime());
        Assert.All(list.Where(a => a.GetProperty("achievementId").GetString() != "win_1"),
            a => Assert.False(a.TryGetProperty("unlockedAt", out _)));
    }

    [Fact]
    public async Task My_achievements_require_a_login()
    {
        var res = await _factory.CreateClient().GetAsync("/api/users/me/achievements");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task My_achievements_are_the_signed_in_users()
    {
        var userId = await SeedUserWithWin1Async("ach-me");

        var res = await _factory.CreateAuthenticatedClient(userId, "ach-me").GetAsync("/api/users/me/achievements");
        var list = (await ReadJsonAsync(res)).EnumerateArray().ToList();

        Assert.Equal(AchievementCatalog.All.Count, list.Count);
        var win1 = list.Single(a => a.GetProperty("achievementId").GetString() == "win_1");
        Assert.Equal(UnlockedAt, win1.GetProperty("unlockedAt").GetDateTime().ToUniversalTime());
    }
}
