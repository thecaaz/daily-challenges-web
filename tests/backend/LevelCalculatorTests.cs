using DailyChallenges.Services;

namespace DailyChallenges.Tests;

public class LevelCalculatorTests
{
    private static LevelCalculator Default() => new LevelCalculator(100, 1.5);

    [Fact]
    public void Level1_RequiresZeroXp_AndNeeds100ToAdvance()
    {
        var calc = Default();
        var (level, xpInto, xpToNext) = calc.GetLevelProgress(0);
        Assert.Equal(1, level);
        Assert.Equal(0, xpInto);
        Assert.Equal(100, xpToNext);
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
    public void GetLevelProgress_AtExactLevelBoundary_AdvancesToNextLevel()
    {
        var calc = Default();
        const long level2Threshold = 100;
        var (level, xpInto, _) = calc.GetLevelProgress(level2Threshold);
        Assert.Equal(2, level);
        Assert.Equal(0, xpInto);
    }

    [Fact]
    public void GetLevelProgress_PartialXpIntoLevel()
    {
        var calc = Default();
        const long level1Cost = 100;
        const long xpIntoLevel2 = 50;
        var (level, xpInto, _) = calc.GetLevelProgress(level1Cost + xpIntoLevel2);
        Assert.Equal(2, level);
        Assert.Equal(xpIntoLevel2, xpInto);
    }

    [Fact]
    public void GetLevelFromTotalXp_MatchesGetLevelProgress()
    {
        var calc = Default();
        for (long xp = 0; xp <= 1000; xp += 50)
        {
            var (expectedLevel, _, _) = calc.GetLevelProgress(xp);
            Assert.Equal(expectedLevel, calc.GetLevelFromTotalXp(xp));
        }
    }

    [Fact]
    public void XpRequiredForLevel_InvalidLevel_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Default().XpRequiredForLevel(0));
    }

    private static LevelCalculator LinearCurveWithBase200() => new(200, 1.0);

    [Theory]
    [InlineData(1, 200)]
    [InlineData(2, 400)]
    [InlineData(5, 1000)]
    public void CustomLinearCurve_XpRequiredForLevel_IsBaseTimesLevel(int level, long expectedCost)
    {
        Assert.Equal(expectedCost, LinearCurveWithBase200().XpRequiredForLevel(level));
    }

    [Fact]
    public void CustomLinearCurve_CostOfLevels1And2_ReachesLevel3()
    {
        const long level1Cost = 200;
        const long level2Cost = 400;

        var (level, _, _) = LinearCurveWithBase200().GetLevelProgress(level1Cost + level2Cost);

        Assert.Equal(3, level);
    }

    [Fact]
    public void GetLevelProgress_VeryHighXp_ResolvesWithoutLooping()
    {
        var calc = Default();
        var (level, _, _) = calc.GetLevelProgress(1_000_000);
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

    /// <summary>round(0.1 * L^0.1) is 0 for every realistic level: each level still costs at least 1 XP.</summary>
    [Fact]
    public void TinyCurve_StillTerminates()
    {
        var calc = new LevelCalculator(0.1, 0.1);
        var (level, _, toNext) = calc.GetLevelProgress(1_000);
        Assert.Equal(1_001, level);
        Assert.Equal(1, toNext);
        Assert.Equal(LevelCalculator.MaxLevel, calc.GetLevelFromTotalXp(long.MaxValue));
    }

    [Fact]
    public void NegativeXp_IsLevel1()
    {
        Assert.Equal(1, Default().GetLevelFromTotalXp(-50));
    }

    [Theory]
    [InlineData(2, 100)]
    [InlineData(3, 383)]
    [InlineData(4, 903)]
    [InlineData(5, 1_703)]
    [InlineData(10, 11_106)]
    [InlineData(20, 67_135)]
    [InlineData(50, 689_513)]
    public void DefaultCurve_DocumentedThresholds(int level, long totalXp)
    {
        var calc = Default();

        Assert.Equal(totalXp, calc.TotalXpForLevel(level));
        Assert.Equal((level, 0L, calc.XpRequiredForLevel(level)), calc.GetLevelProgress(totalXp));
        Assert.Equal((level - 1, calc.XpRequiredForLevel(level - 1) - 1, 1L), calc.GetLevelProgress(totalXp - 1));
    }
}

