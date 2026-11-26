using DailyChallenges.Data;
using DailyChallenges.IntegrationTests.Infrastructure;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services.Ranking;

namespace DailyChallenges.IntegrationTests;

public class EfSubmissionRepositoryTests
{
    private static readonly DateTime Day1 = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = new(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc);

    private static Submission SeedSub(AppDbContext ctx, int gameId, double? scoreValue, DateTime scoringDay, int? userId = null)
    {
        var sub = new Submission
        {
            GameId = gameId,
            Score = scoreValue?.ToString() ?? "x",
            ScoreValue = scoreValue,
            ScoringDay = scoringDay.Date,
            UserId = userId,
            Username = userId.HasValue ? $"user{userId}" : "anon"
        };
        ctx.Submissions.Add(sub);
        ctx.SaveChanges();
        return sub;
    }

    // ── GetByGameAndDayByScoreValueAsync ──────────────────────────────────────

    [Fact]
    public async Task GetByGameAndDay_HighestStrategy_OrdersDescending()
    {
        await using var db = new TestDb();
        db.Ctx.SeedGame(1);
        SeedSub(db.Ctx, 1, 100, Day1);
        SeedSub(db.Ctx, 1, 300, Day1);
        SeedSub(db.Ctx, 1, 200, Day1);
        var repo = new EfSubmissionRepository(db.Ctx);

        var results = await repo.GetByGameAndDayByScoreValueAsync(1, Day1, new HighestRankingStrategy(), CancellationToken.None);

        Assert.Equal(3, results.Count);
        Assert.Equal(300.0, results[0].ScoreValue);
        Assert.Equal(200.0, results[1].ScoreValue);
        Assert.Equal(100.0, results[2].ScoreValue);
    }

    [Fact]
    public async Task GetByGameAndDay_LowestStrategy_OrdersAscending()
    {
        await using var db = new TestDb();
        db.Ctx.SeedGame(1, RankingMode.Lowest);
        SeedSub(db.Ctx, 1, 300, Day1);
        SeedSub(db.Ctx, 1, 100, Day1);
        SeedSub(db.Ctx, 1, 200, Day1);
        var repo = new EfSubmissionRepository(db.Ctx);

        var results = await repo.GetByGameAndDayByScoreValueAsync(1, Day1, new LowestRankingStrategy(), CancellationToken.None);

        Assert.Equal(100.0, results[0].ScoreValue);
        Assert.Equal(200.0, results[1].ScoreValue);
        Assert.Equal(300.0, results[2].ScoreValue);
    }

    [Fact]
    public async Task GetByGameAndDay_ExcludesOtherGame()
    {
        await using var db = new TestDb();
        db.Ctx.SeedGame(1);
        db.Ctx.SeedGame(2);
        SeedSub(db.Ctx, 1, 100, Day1);
        SeedSub(db.Ctx, 2, 999, Day1);
        var repo = new EfSubmissionRepository(db.Ctx);

        var results = await repo.GetByGameAndDayByScoreValueAsync(1, Day1, new HighestRankingStrategy(), CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(100.0, results[0].ScoreValue);
    }

    [Fact]
    public async Task GetByGameAndDay_ExcludesOtherDay()
    {
        await using var db = new TestDb();
        db.Ctx.SeedGame(1);
        SeedSub(db.Ctx, 1, 100, Day1);
        SeedSub(db.Ctx, 1, 200, Day2);
        var repo = new EfSubmissionRepository(db.Ctx);

        var results = await repo.GetByGameAndDayByScoreValueAsync(1, Day1, new HighestRankingStrategy(), CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(100.0, results[0].ScoreValue);
    }

    [Fact]
    public async Task GetByGameAndDay_ExcludesNullScoreValue()
    {
        await using var db = new TestDb();
        db.Ctx.SeedGame(1);
        SeedSub(db.Ctx, 1, null, Day1);
        SeedSub(db.Ctx, 1, 100, Day1);
        var repo = new EfSubmissionRepository(db.Ctx);

        var results = await repo.GetByGameAndDayByScoreValueAsync(1, Day1, new HighestRankingStrategy(), CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(100.0, results[0].ScoreValue);
    }

    // ── GetWinnerForGameAndDayAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetWinner_NoScoredSubmissions_ReturnsNull()
    {
        await using var db = new TestDb();
        db.Ctx.SeedGame(1);
        SeedSub(db.Ctx, 1, null, Day1);
        var repo = new EfSubmissionRepository(db.Ctx);

        var winner = await repo.GetWinnerForGameAndDayAsync(1, Day1, new HighestRankingStrategy(), CancellationToken.None);

        Assert.Null(winner);
    }

    [Fact]
    public async Task GetWinner_Highest_ReturnsTopScore()
    {
        await using var db = new TestDb();
        db.Ctx.SeedGame(1);
        SeedSub(db.Ctx, 1, 100, Day1);
        SeedSub(db.Ctx, 1, 300, Day1);
        SeedSub(db.Ctx, 1, 200, Day1);
        var repo = new EfSubmissionRepository(db.Ctx);

        var winner = await repo.GetWinnerForGameAndDayAsync(1, Day1, new HighestRankingStrategy(), CancellationToken.None);

        Assert.Equal(300.0, winner?.ScoreValue);
    }

    [Fact]
    public async Task GetWinner_Lowest_ReturnsLowestScore()
    {
        await using var db = new TestDb();
        db.Ctx.SeedGame(1, RankingMode.Lowest);
        SeedSub(db.Ctx, 1, 300, Day1);
        SeedSub(db.Ctx, 1, 100, Day1);
        SeedSub(db.Ctx, 1, 200, Day1);
        var repo = new EfSubmissionRepository(db.Ctx);

        var winner = await repo.GetWinnerForGameAndDayAsync(1, Day1, new LowestRankingStrategy(), CancellationToken.None);

        Assert.Equal(100.0, winner?.ScoreValue);
    }

    // ── GetWinnersForGameAndDaysAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetWinnersForDays_ReturnsOneWinnerPerDay()
    {
        await using var db = new TestDb();
        db.Ctx.SeedGame(1);
        SeedSub(db.Ctx, 1, 200, Day1);
        SeedSub(db.Ctx, 1, 100, Day1);
        SeedSub(db.Ctx, 1, 150, Day2);
        SeedSub(db.Ctx, 1, 50, Day2);
        var repo = new EfSubmissionRepository(db.Ctx);

        var winners = await repo.GetWinnersForGameAndDaysAsync(1, new List<DateTime> { Day1, Day2 }, new HighestRankingStrategy(), CancellationToken.None);

        Assert.Equal(2, winners.Count);
        Assert.Contains(winners, w => w.ScoringDay.Date == Day1.Date && w.ScoreValue == 200.0);
        Assert.Contains(winners, w => w.ScoringDay.Date == Day2.Date && w.ScoreValue == 150.0);
    }

    [Fact]
    public async Task GetWinnersForDays_EmptyDayList_ReturnsEmpty()
    {
        await using var db = new TestDb();
        db.Ctx.SeedGame(1);
        SeedSub(db.Ctx, 1, 100, Day1);
        var repo = new EfSubmissionRepository(db.Ctx);

        var winners = await repo.GetWinnersForGameAndDaysAsync(1, new List<DateTime>(), new HighestRankingStrategy(), CancellationToken.None);

        Assert.Empty(winners);
    }

    // ── CountByUserAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task CountByUser_ReturnsCorrectCount()
    {
        await using var db = new TestDb();
        db.Ctx.SeedGame(1);
        db.Ctx.SeedUser(1);
        SeedSub(db.Ctx, 1, 100, Day1, userId: 1);
        SeedSub(db.Ctx, 1, 200, Day2, userId: 1);
        var anonymous = (int?)null;
        SeedSub(db.Ctx, 1, 300, Day1, userId: anonymous);
        var repo = new EfSubmissionRepository(db.Ctx);

        var count = await repo.CountByUserAsync(1, CancellationToken.None);

        Assert.Equal(2, count);
    }

    // ── GetAvailableDatesAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailableDates_CollapsesSameDayPlays_AndReturnsDatesDescending()
    {
        await using var db = new TestDb();
        db.Ctx.SeedGame(1);
        SeedSub(db.Ctx, 1, 100, Day1);
        SeedSub(db.Ctx, 1, 200, Day1);
        SeedSub(db.Ctx, 1, 300, Day2);
        var repo = new EfSubmissionRepository(db.Ctx);

        var dates = await repo.GetAvailableDatesAsync(1, CancellationToken.None);

        Assert.Equal(2, dates.Count);
        Assert.Equal(Day2.Date, dates[0].Date);
        Assert.Equal(Day1.Date, dates[1].Date);
    }

    // ── CountOnGameDaysAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task CountOnGameDays_CountsOnlyEachRequestedGamesOwnDay()
    {
        await using var db = new TestDb();
        db.Ctx.SeedGame(1);
        db.Ctx.SeedGame(2);
        db.Ctx.SeedGame(3);
        SeedSub(db.Ctx, 1, 1, Day1);
        SeedSub(db.Ctx, 1, 2, Day1);
        SeedSub(db.Ctx, 1, 3, Day2);
        SeedSub(db.Ctx, 2, 4, Day1);
        SeedSub(db.Ctx, 3, 5, Day1);
        var repo = new EfSubmissionRepository(db.Ctx);
        var currentDayByGame = new Dictionary<int, DateTime> { [1] = Day1, [2] = Day2 };

        var (activeGames, submissions) = await repo.CountOnGameDaysAsync(currentDayByGame, CancellationToken.None);

        Assert.Equal(1, activeGames);
        Assert.Equal(2, submissions);
    }
}
