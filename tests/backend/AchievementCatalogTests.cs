using DailyChallenges.Achievements;

namespace DailyChallenges.Tests;

public class AchievementCatalogTests
{
    // ── Catalog completeness ──────────────────────────────────────────────────

    [Fact]
    public void All_Contains15Achievements()
    {
        Assert.Equal(13, AchievementCatalog.All.Count);
    }

    [Fact]
    public void All_AllIdsAreUnique()
    {
        var ids = AchievementCatalog.All.Select(a => a.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void All_NoDefinitionHasNullOrEmptyFields()
    {
        foreach (var def in AchievementCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(def.Id),       $"Id blank on {def.Id}");
            Assert.False(string.IsNullOrWhiteSpace(def.Name),     $"Name blank on {def.Id}");
            Assert.False(string.IsNullOrWhiteSpace(def.Description), $"Description blank on {def.Id}");
            Assert.False(string.IsNullOrWhiteSpace(def.IconKey),  $"IconKey blank on {def.Id}");
        }
    }

    // ── Expected IDs present ──────────────────────────────────────────────────

    [Theory]
    [InlineData("submission_first")]
    [InlineData("submission_50")]
    [InlineData("submission_250")]
    [InlineData("streak_7")]
    [InlineData("streak_30")]
    [InlineData("streak_100")]
    [InlineData("win_1")]
    [InlineData("win_10")]
    [InlineData("win_50")]
    [InlineData("level_5")]
    [InlineData("level_10")]
    [InlineData("level_25")]
    [InlineData("first_friend")]
    public void All_ContainsExpectedId(string id)
    {
        Assert.Contains(AchievementCatalog.All, a => a.Id == id);
    }

    // ── GetById ───────────────────────────────────────────────────────────────

    [Fact]
    public void GetById_KnownId_ReturnsCorrectDefinition()
    {
        var def = AchievementCatalog.GetById("win_1");
        Assert.NotNull(def);
        Assert.Equal("win_1", def.Id);
        Assert.Equal("Winner", def.Name);
    }

    [Fact]
    public void GetById_UnknownId_ReturnsNull()
    {
        var def = AchievementCatalog.GetById("does_not_exist");
        Assert.Null(def);
    }

    [Fact]
    public void GetById_EmptyString_ReturnsNull()
    {
        Assert.Null(AchievementCatalog.GetById(""));
    }

    [Fact]
    public void GetById_AllEntriesLookUpSuccessfully()
    {
        foreach (var def in AchievementCatalog.All)
        {
            var found = AchievementCatalog.GetById(def.Id);
            Assert.NotNull(found);
            Assert.Equal(def.Id, found.Id);
        }
    }

    // ── Category groupings ────────────────────────────────────────────────────

    [Fact]
    public void All_ThreeSubmissionAchievements()
    {
        Assert.Equal(3, AchievementCatalog.All.Count(a => a.Id.StartsWith("submission_")));
    }

    [Fact]
    public void All_ThreeStreakAchievements()
    {
        Assert.Equal(3, AchievementCatalog.All.Count(a => a.Id.StartsWith("streak_")));
    }

    [Fact]
    public void All_ThreeWinAchievements()
    {
        Assert.Equal(3, AchievementCatalog.All.Count(a => a.Id.StartsWith("win_")));
    }

    [Fact]
    public void All_ThreeLevelAchievements()
    {
        Assert.Equal(3, AchievementCatalog.All.Count(a => a.Id.StartsWith("level_")));
    }

    [Fact]
    public void All_OneSocialAchievement()
    {
        Assert.Equal(1, AchievementCatalog.All.Count(a => a.Id == "first_friend"));
    }
}
