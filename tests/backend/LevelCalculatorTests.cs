using DailyChallenges.Services;

namespace DailyChallenges.Tests;

public class LevelCalculatorTests
{
    private static LevelCalculator Default() => new LevelCalculator(100, 1.5);

    [Fact]
    public void Level1_RequiresZeroXp()
    {
        var calc = Default();
        var (level, xpInto, xpToNext) = calc.GetLevelInfo(0);
        Assert.Equal(1, level);
        Assert.Equal(0, xpInto);
        Assert.Equal(100, xpToNext); // level 1 needs 100 XP to advance
    }

    [Fact]
    public void XpRequiredForLevel1_Is100()
    {
        Assert.Equal(100, Default().XpRequiredForLevel(1));
    }

    [Fact]
    public void TotalXpForLevel1_IsZero()
    {
        Assert.Equal(0, Default().TotalXpForLevel(1));
    }

    [Fact]
    public void TotalXpForLevel2_Is100()
    {
        Assert.Equal(100, Default().TotalXpForLevel(2));
    }

    [Fact]
    public void GetLevelInfo_AtExactLevelBoundary_AdvancesToNextLevel()
    {
        var calc = Default();
        // Exactly 100 XP puts us at level 2 with 0 XP into it
        var (level, xpInto, xpToNext) = calc.GetLevelInfo(100);
        Assert.Equal(2, level);
        Assert.Equal(0, xpInto);
    }

    [Fact]
    public void GetLevelInfo_PartialXpIntoLevel()
    {
        var calc = Default();
        // 150 XP: 100 completes level 1, 50 into level 2
        var (level, xpInto, _) = calc.GetLevelInfo(150);
        Assert.Equal(2, level);
        Assert.Equal(50, xpInto);
    }

    [Fact]
    public void GetLevelFromTotalXp_MatchesGetLevelInfo()
    {
        var calc = Default();
        for (long xp = 0; xp <= 1000; xp += 50)
        {
            var (expectedLevel, _, _) = calc.GetLevelInfo(xp);
            Assert.Equal(expectedLevel, calc.GetLevelFromTotalXp(xp));
        }
    }

    [Fact]
    public void XpRequiredForLevel_InvalidLevel_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Default().XpRequiredForLevel(0));
    }

    [Fact]
    public void CustomCurve_AffectsThresholds()
    {
        // With base=200 and exponent=1.0: cost(n) = 200 * n
        // cost(1)=200, cost(2)=400, so total to reach level 3 = 600 XP
        var calc = new LevelCalculator(200, 1.0);
        Assert.Equal(200, calc.XpRequiredForLevel(1));
        Assert.Equal(400, calc.XpRequiredForLevel(2)); // 200 * 2
        Assert.Equal(1000, calc.XpRequiredForLevel(5)); // 200 * 5
        var (level, _, _) = calc.GetLevelInfo(600); // 200+400=600 → level 3
        Assert.Equal(3, level);
    }

    [Fact]
    public void HighXp_DoesNotInfiniteLoop()
    {
        var calc = Default();
        // Smoke test: very high XP should resolve quickly
        var (level, _, _) = calc.GetLevelInfo(1_000_000);
        Assert.True(level > 50);
    }

    // ── Bad configuration ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 1.5)]
    [InlineData(-100, 1.5)]
    [InlineData(100, 0)]
    [InlineData(100, -1)]
    [InlineData(double.NaN, 1.5)]
    [InlineData(100, double.PositiveInfinity)]
    public void Constructor_RejectsInvalidCurve(double levelBase, double levelExponent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LevelCalculator(levelBase, levelExponent));
    }

    [Theory]
    [InlineData(0, 1.5, false)]
    [InlineData(100, -0.5, false)]
    [InlineData(100, 1.5, true)]
    public void XpConfigValidator_ChecksTheCurve(double levelBase, double levelExponent, bool valid)
    {
        var result = new XpConfigValidator().Validate(null, new XpConfig { LevelBase = levelBase, LevelExponent = levelExponent });
        Assert.Equal(valid, result.Succeeded);
    }

    [Fact]
    public void TinyCurve_StillTerminates()
    {
        // round(0.1 * L^0.1) is 0 for every realistic level: each level still costs at least 1 XP.
        var calc = new LevelCalculator(0.1, 0.1);
        var (level, _, toNext) = calc.GetLevelInfo(1_000);
        Assert.Equal(1_001, level);
        Assert.Equal(1, toNext);
        Assert.Equal(LevelCalculator.MaxLevel, calc.GetLevelFromTotalXp(long.MaxValue));
    }

    [Fact]
    public void NegativeXp_IsLevel1()
    {
        Assert.Equal(1, Default().GetLevelFromTotalXp(-50));
    }
}
