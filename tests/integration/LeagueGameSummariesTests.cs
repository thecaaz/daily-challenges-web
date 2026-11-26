using DailyChallenges.Data;
using DailyChallenges.DTOs;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services;
using DailyChallenges.TestUtilities;
using Microsoft.Extensions.Options;

namespace DailyChallenges.IntegrationTests;

/// <summary>League game summaries through <see cref="LeagueStandingsService"/> on real SQLite repositories.</summary>
public class LeagueGameSummariesTests
{
    private static LeagueStandingsService CreateService(AppDbContext db, TimeProvider time)
    {
        var games = new EfGameRepository(db);
        var submissions = new EfSubmissionRepository(db);
        var visibility = new ScoreVisibilityService(new UserSubmissionChecker(submissions, games, time), time);
        var signer = new ScreenshotUrlSigner(Options.Create(new JwtOptions { Key = "league-game-summaries-test-key-32-chars-long" }), time);
        return new LeagueStandingsService(new EfLeagueRepository(db), new EfLeagueStandingsRepository(db), games, visibility, signer, time);
    }

    private static void SeedLeague(AppDbContext db, DateTime createdAt, params int[] memberIds)
    {
        foreach (var id in memberIds) db.Users.Add(new User { Id = id, Username = $"user{id}" });
        db.Leagues.Add(new League { Id = 1, OwnerId = memberIds[0], Name = "L1", CreatedAt = createdAt });
        db.SaveChanges();
        foreach (var id in memberIds)
            db.LeagueMembers.Add(new LeagueMember { LeagueId = 1, UserId = id, Role = id == memberIds[0] ? LeagueRole.Owner : LeagueRole.Member, JoinedAt = createdAt });
        db.SaveChanges();
    }

    private static void SeedSubmission(AppDbContext db, int userId, double scoreValue, DateTime scoringDay)
    {
        db.Submissions.Add(new Submission
        {
            GameId = 1, UserId = userId, Username = $"user{userId}", Score = scoreValue.ToString(),
            ScoreValue = scoreValue, ScoringDay = scoringDay.Date, CreatedAt = scoringDay.Date,
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task GetLeagueGameSummaries_ReturnsAggregatesAndUserRank()
    {
        await using var db = new TestDb();
        SeedLeague(db.Ctx, DateTime.UtcNow, 1, 2);
        db.Ctx.Games.Add(new Game { Id = 1, Name = "Game1", RankingMode = RankingMode.Highest });
        db.Ctx.SaveChanges();
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var today = DateTime.UtcNow.Date;
        SeedSubmission(db.Ctx, userId: 1, 100, yesterday);
        SeedSubmission(db.Ctx, userId: 2, 200, yesterday);
        SeedSubmission(db.Ctx, userId: 1, 300, today);

        var (items, total) = await CreateService(db.Ctx, TimeProvider.System)
            .GetLeagueGameSummariesAsync(new LeagueGameSummaryQuery(1, RequestingUserId: 1, Days: 2, Page: 1, PageSize: 20), CancellationToken.None);

        Assert.Equal(1, total);
        var summary = Assert.Single(items);
        Assert.Equal(1, summary.GameId);
        Assert.Equal(3, summary.PlayCount);
        Assert.Equal(300.0, summary.TopScoreValue);
        Assert.Equal(300.0, summary.MyBestScoreValue);
        Assert.Equal(1, summary.MyRank);
        Assert.Equal(new List<int> { 2, 1 }, summary.RecentPlays);
    }

    /// <summary>Each game's window ends at its own current scoring day (B8).</summary>
    [Fact]
    public async Task Late_reset_game_keeps_todays_plays_in_the_summary()
    {
        await using var db = new TestDb();
        var lateEvening = new DateTime(2026, 6, 15, 23, 0, 0, DateTimeKind.Utc);
        SeedLeague(db.Ctx, lateEvening, 1);
        db.Ctx.Games.Add(new Game { Id = 1, Name = "Late", ResetTime = TimeSpan.FromHours(22) });
        db.Ctx.Submissions.Add(new Submission
        {
            GameId = 1, UserId = 1, Username = "user1", Score = "5", ScoreValue = 5,
            CreatedAt = lateEvening.AddMinutes(-30), ScoringDay = new DateTime(2026, 6, 16), ScreenshotContentType = "image/png",
        });
        db.Ctx.SaveChanges();

        var (items, _) = await CreateService(db.Ctx, new FixedTimeProvider(lateEvening))
            .GetLeagueGameSummariesAsync(new LeagueGameSummaryQuery(1, RequestingUserId: 1, Days: 3, Page: 1, PageSize: 20), CancellationToken.None);

        var summary = Assert.Single(items);
        Assert.Equal(1, summary.PlayCount);
        Assert.Equal(new List<int> { 0, 0, 1 }, summary.RecentPlays);
    }
}
