using DailyChallenges.Models;
using DailyChallenges.Services.Ranking;

namespace DailyChallenges.Tests;

public class RankingStrategyTests
{
    private static Submission Sub(int id, int? scoreValue, DateTime createdAt) =>
        new() { Id = id, GameId = 1, UserId = id, Score = scoreValue?.ToString() ?? "", ScoreValue = scoreValue, CreatedAt = createdAt };

    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    // ── HighestRankingStrategy ────────────────────────────────────────────────

    [Fact]
    public void Highest_OrdersByScoreValueDescending()
    {
        var subs = new[] { Sub(1, 10, T0), Sub(2, 50, T0), Sub(3, 30, T0) }.AsQueryable();
        var result = new HighestRankingStrategy().ApplyOrdering(subs).ToList();
        Assert.Equal([50, 30, 10], result.Select(s => s.ScoreValue));
    }

    [Fact]
    public void Highest_TiesBrokenByEarliestCreatedAt()
    {
        var subs = new[]
        {
            Sub(1, 100, T0.AddMinutes(5)),
            Sub(2, 100, T0),
            Sub(3, 100, T0.AddMinutes(2)),
        }.AsQueryable();
        var result = new HighestRankingStrategy().ApplyOrdering(subs).ToList();
        Assert.Equal([2, 3, 1], result.Select(s => s.Id));
    }

    [Fact]
    public void Highest_NullScoreValueOrderedLast()
    {
        var subs = new[] { Sub(1, null, T0), Sub(2, 50, T0), Sub(3, null, T0) }.AsQueryable();
        var result = new HighestRankingStrategy().ApplyOrdering(subs).ToList();
        Assert.Equal(50, result[0].ScoreValue);
    }

    [Fact]
    public void Highest_SingleItem_ReturnsItself()
    {
        var subs = new[] { Sub(1, 42, T0) }.AsQueryable();
        var result = new HighestRankingStrategy().ApplyOrdering(subs).ToList();
        Assert.Single(result);
        Assert.Equal(42, result[0].ScoreValue);
    }

    [Fact]
    public void Highest_EmptyInput_ReturnsEmpty()
    {
        var result = new HighestRankingStrategy().ApplyOrdering(Enumerable.Empty<Submission>().AsQueryable()).ToList();
        Assert.Empty(result);
    }

    // ── LowestRankingStrategy ─────────────────────────────────────────────────

    [Fact]
    public void Lowest_OrdersByScoreValueAscending()
    {
        var subs = new[] { Sub(1, 30, T0), Sub(2, 10, T0), Sub(3, 50, T0) }.AsQueryable();
        var result = new LowestRankingStrategy().ApplyOrdering(subs).ToList();
        Assert.Equal([10, 30, 50], result.Select(s => s.ScoreValue));
    }

    [Fact]
    public void Lowest_TiesBrokenByEarliestCreatedAt()
    {
        var subs = new[]
        {
            Sub(1, 5, T0.AddMinutes(5)),
            Sub(2, 5, T0),
            Sub(3, 5, T0.AddMinutes(2)),
        }.AsQueryable();
        var result = new LowestRankingStrategy().ApplyOrdering(subs).ToList();
        Assert.Equal([2, 3, 1], result.Select(s => s.Id));
    }

    [Fact]
    public void Lowest_NullScoreValueOrderedFirst()
    {
        // LINQ orders nulls first for ascending
        var subs = new[] { Sub(1, 50, T0), Sub(2, null, T0), Sub(3, 10, T0) }.AsQueryable();
        var result = new LowestRankingStrategy().ApplyOrdering(subs).ToList();
        Assert.Null(result[0].ScoreValue);
    }

    // ── RankingStrategyFactory ────────────────────────────────────────────────

    [Fact]
    public void Factory_Highest_ReturnsHighestRankingStrategy()
    {
        var strategy = RankingStrategyFactory.GetStrategy(RankingMode.Highest);
        Assert.IsType<HighestRankingStrategy>(strategy);
    }

    [Fact]
    public void Factory_Lowest_ReturnsLowestRankingStrategy()
    {
        var strategy = RankingStrategyFactory.GetStrategy(RankingMode.Lowest);
        Assert.IsType<LowestRankingStrategy>(strategy);
    }

    [Fact]
    public void Factory_DefaultEnum_ReturnsHighestRankingStrategy()
    {
        var strategy = RankingStrategyFactory.GetStrategy(default);
        Assert.IsType<HighestRankingStrategy>(strategy);
    }
}
