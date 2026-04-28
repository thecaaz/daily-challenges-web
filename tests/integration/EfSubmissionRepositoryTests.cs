using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services.Ranking;

namespace DailyChallenges.IntegrationTests;

public class EfSubmissionRepositoryTests
{
    private static readonly DateTime Day1 = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = new(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc);

    private static void SeedGame(AppDbContext ctx, int id, RankingMode mode = RankingMode.Highest)
    {
        ctx.Games.Add(new Game { Id = id, Name = $"Game{id}", RankingMode = mode });
        ctx.SaveChanges();
    }

    private static void SeedUser(AppDbContext ctx, int id)
    {
        ctx.Users.Add(new User { Id = id, Username = $"user{id}" });
        ctx.SaveChanges();
    }

    private static Submission SeedSub(AppDbContext ctx, int gameId, int? scoreValue, DateTime scoringDay, int? userId = null)
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
        SeedGame(db.Ctx, 1);
        SeedSub(db.Ctx, 1, 100, Day1);
        SeedSub(db.Ctx, 1, 300, Day1);
        SeedSub(db.Ctx, 1, 200, Day1);
        var repo = new EfSubmissionRepository(db.Ctx);

        var results = await repo.GetByGameAndDayByScoreValueAsync(1, Day1, new HighestRankingStrategy());

        Assert.Equal(3, results.Count);
        Assert.Equal(300, results[0].ScoreValue);
        Assert.Equal(200, results[1].ScoreValue);
        Assert.Equal(100, results[2].ScoreValue);
    }

    [Fact]
    public async Task GetByGameAndDay_LowestStrategy_OrdersAscending()
    {
        await using var db = new TestDb();
        SeedGame(db.Ctx, 1, RankingMode.Lowest);
        SeedSub(db.Ctx, 1, 300, Day1);
        SeedSub(db.Ctx, 1, 100, Day1);
        SeedSub(db.Ctx, 1, 200, Day1);
        var repo = new EfSubmissionRepository(db.Ctx);

        var results = await repo.GetByGameAndDayByScoreValueAsync(1, Day1, new LowestRankingStrategy());

        Assert.Equal(100, results[0].ScoreValue);
        Assert.Equal(200, results[1].ScoreValue);
        Assert.Equal(300, results[2].ScoreValue);
    }

    [Fact]
    public async Task GetByGameAndDay_ExcludesOtherGame()
    {
        await using var db = new TestDb();
        SeedGame(db.Ctx, 1);
        SeedGame(db.Ctx, 2);
        SeedSub(db.Ctx, 1, 100, Day1);
        SeedSub(db.Ctx, 2, 999, Day1);
        var repo = new EfSubmissionRepository(db.Ctx);

        var results = await repo.GetByGameAndDayByScoreValueAsync(1, Day1, new HighestRankingStrategy());

        Assert.Single(results);
        Assert.Equal(100, results[0].ScoreValue);
    }

    [Fact]
    public async Task GetByGameAndDay_ExcludesOtherDay()
    {
        await using var db = new TestDb();
        SeedGame(db.Ctx, 1);
        SeedSub(db.Ctx, 1, 100, Day1);
        SeedSub(db.Ctx, 1, 200, Day2);
        var repo = new EfSubmissionRepository(db.Ctx);

        var results = await repo.GetByGameAndDayByScoreValueAsync(1, Day1, new HighestRankingStrategy());

        Assert.Single(results);
        Assert.Equal(100, results[0].ScoreValue);
    }

    [Fact]
    public async Task GetByGameAndDay_ExcludesNullScoreValue()
    {
        await using var db = new TestDb();
        SeedGame(db.Ctx, 1);
        SeedSub(db.Ctx, 1, null, Day1);
        SeedSub(db.Ctx, 1, 100, Day1);
        var repo = new EfSubmissionRepository(db.Ctx);

        var results = await repo.GetByGameAndDayByScoreValueAsync(1, Day1, new HighestRankingStrategy());

        Assert.Single(results);
        Assert.Equal(100, results[0].ScoreValue);
    }

    // ── GetWinnerForGameAndDayAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetWinner_NoScoredSubmissions_ReturnsNull()
    {
        await using var db = new TestDb();
        SeedGame(db.Ctx, 1);
        SeedSub(db.Ctx, 1, null, Day1);
        var repo = new EfSubmissionRepository(db.Ctx);

        var winner = await repo.GetWinnerForGameAndDayAsync(1, Day1, new HighestRankingStrategy());

        Assert.Null(winner);
    }

    [Fact]
    public async Task GetWinner_Highest_ReturnsTopScore()
    {
        await using var db = new TestDb();
        SeedGame(db.Ctx, 1);
        SeedSub(db.Ctx, 1, 100, Day1);
        SeedSub(db.Ctx, 1, 300, Day1);
        SeedSub(db.Ctx, 1, 200, Day1);
        var repo = new EfSubmissionRepository(db.Ctx);

        var winner = await repo.GetWinnerForGameAndDayAsync(1, Day1, new HighestRankingStrategy());

        Assert.Equal(300, winner?.ScoreValue);
    }

    [Fact]
    public async Task GetWinner_Lowest_ReturnsLowestScore()
    {
        await using var db = new TestDb();
        SeedGame(db.Ctx, 1, RankingMode.Lowest);
        SeedSub(db.Ctx, 1, 300, Day1);
        SeedSub(db.Ctx, 1, 100, Day1);
        SeedSub(db.Ctx, 1, 200, Day1);
        var repo = new EfSubmissionRepository(db.Ctx);

        var winner = await repo.GetWinnerForGameAndDayAsync(1, Day1, new LowestRankingStrategy());

        Assert.Equal(100, winner?.ScoreValue);
    }

    // ── GetWinnersForGameAndDaysAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetWinnersForDays_ReturnsOneWinnerPerDay()
    {
        await using var db = new TestDb();
        SeedGame(db.Ctx, 1);
        SeedSub(db.Ctx, 1, 200, Day1);
        SeedSub(db.Ctx, 1, 100, Day1);
        SeedSub(db.Ctx, 1, 150, Day2);
        SeedSub(db.Ctx, 1, 50, Day2);
        var repo = new EfSubmissionRepository(db.Ctx);

        var winners = await repo.GetWinnersForGameAndDaysAsync(1, new List<DateTime> { Day1, Day2 }, new HighestRankingStrategy());

        Assert.Equal(2, winners.Count);
        Assert.Contains(winners, w => w.ScoringDay.Date == Day1.Date && w.ScoreValue == 200);
        Assert.Contains(winners, w => w.ScoringDay.Date == Day2.Date && w.ScoreValue == 150);
    }

    [Fact]
    public async Task GetWinnersForDays_EmptyDayList_ReturnsEmpty()
    {
        await using var db = new TestDb();
        SeedGame(db.Ctx, 1);
        SeedSub(db.Ctx, 1, 100, Day1);
        var repo = new EfSubmissionRepository(db.Ctx);

        var winners = await repo.GetWinnersForGameAndDaysAsync(1, new List<DateTime>(), new HighestRankingStrategy());

        Assert.Empty(winners);
    }

    // ── CountByUserAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task CountByUser_ReturnsCorrectCount()
    {
        await using var db = new TestDb();
        SeedGame(db.Ctx, 1);
        SeedUser(db.Ctx, 1);
        SeedSub(db.Ctx, 1, 100, Day1, userId: 1);
        SeedSub(db.Ctx, 1, 200, Day2, userId: 1);
        SeedSub(db.Ctx, 1, 300, Day1, userId: null); // anonymous — not counted for user 1
        var repo = new EfSubmissionRepository(db.Ctx);

        var count = await repo.CountByUserAsync(1);

        Assert.Equal(2, count);
    }

    // ── GetAvailableDatesAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailableDates_ReturnsDistinctDatesDescending()
    {
        await using var db = new TestDb();
        SeedGame(db.Ctx, 1);
        SeedSub(db.Ctx, 1, 100, Day1);
        SeedSub(db.Ctx, 1, 200, Day1); // duplicate — should collapse
        SeedSub(db.Ctx, 1, 300, Day2);
        var repo = new EfSubmissionRepository(db.Ctx);

        var dates = await repo.GetAvailableDatesAsync(1);

        Assert.Equal(2, dates.Count);
        Assert.Equal(Day2.Date, dates[0].Date);
        Assert.Equal(Day1.Date, dates[1].Date);
    }
}
