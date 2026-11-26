using DailyChallenges.DTOs;
using DailyChallenges.Data;
using DailyChallenges.IntegrationTests.Infrastructure;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;

namespace DailyChallenges.IntegrationTests;

public class EfLeagueStandingsRepositoryTests
{
    private static Submission SeedSub(AppDbContext ctx, int gameId, double? scoreValue, DateTime scoringDay, int? userId = null)
    {
        var sub = new Submission
        {
            GameId = gameId,
            Score = scoreValue?.ToString() ?? "x",
            ScoreValue = scoreValue,
            ScoringDay = scoringDay.Date,
            UserId = userId,
            Username = userId.HasValue ? $"user{userId}" : "anon",
            CreatedAt = scoringDay.Date
        };
        ctx.Submissions.Add(sub);
        ctx.SaveChanges();
        return sub;
    }

    [Fact]
    public async Task GetMemberSubmissions_returns_members_rows_within_the_inclusive_day_range()
    {
        await using var db = new TestDb();
        foreach (var id in new[] { 1, 2, 3 }) db.Ctx.SeedUser(id);
        db.Ctx.Leagues.Add(new League { Id = 1, OwnerId = 1, Name = "L1" });
        db.Ctx.SaveChanges();
        db.Ctx.LeagueMembers.Add(new LeagueMember { LeagueId = 1, UserId = 1 });
        db.Ctx.LeagueMembers.Add(new LeagueMember { LeagueId = 1, UserId = 2 });
        db.Ctx.SaveChanges();
        db.Ctx.SeedGame(1, RankingMode.Lowest);
        var first = new DateTime(2026, 6, 14);
        var last = new DateTime(2026, 6, 16);
        SeedSub(db.Ctx, 1, 10, first, userId: 1);
        SeedSub(db.Ctx, 1, null, last, userId: 2);
        SeedSub(db.Ctx, 1, 30, first.AddDays(-1), userId: 1);
        SeedSub(db.Ctx, 1, 40, last.AddDays(1), userId: 2);
        SeedSub(db.Ctx, 1, 50, first, userId: 3);
        SeedSub(db.Ctx, 1, 60, first);

        var rows = await new EfLeagueStandingsRepository(db.Ctx).GetMemberSubmissionsAsync(1, first, last);

        Assert.Equal(new[] { (1, first), (2, last) }, rows.Select(r => (r.UserId, r.ScoringDay)).OrderBy(r => r.UserId));
        Assert.All(rows, r => Assert.Equal(("Game1", RankingMode.Lowest, false), (r.GameName, r.GameRankingMode, r.GameHasIcon)));
    }

    [Fact]
    public async Task GetMemberSubmissions_of_a_league_without_members_is_empty()
    {
        await using var db = new TestDb();
        db.Ctx.SeedUser(1);
        db.Ctx.SeedGame(1);
        var day = new DateTime(2026, 6, 15);
        SeedSub(db.Ctx, 1, 10, day, userId: 1);

        Assert.Empty(await new EfLeagueStandingsRepository(db.Ctx).GetMemberSubmissionsAsync(1, day, day));
    }

    [Theory]
    [InlineData(RankingMode.Lowest)]
    [InlineData(RankingMode.Highest)]
    public async Task GetLeaderboard_orders_by_the_strategy_with_competition_ranks(RankingMode mode)
    {
        await using var db = new TestDb();
        foreach (var id in new[] { 1, 2, 3, 4 }) db.Ctx.SeedUser(id);
        db.Ctx.Leagues.Add(new League { Id = 1, OwnerId = 1, Name = "L1" });
        db.Ctx.SaveChanges();
        foreach (var id in new[] { 1, 2, 3, 4 })
            db.Ctx.LeagueMembers.Add(new LeagueMember { LeagueId = 1, UserId = id });
        db.Ctx.SaveChanges();
        db.Ctx.SeedGame(1, mode);

        var day = new DateTime(2026, 6, 15);
        var lowWins = mode == RankingMode.Lowest;
        // users 1 and 2 tie for best (10 when lowest wins, 40 when highest wins); user3: 20; user4: 30
        SeedSub(db.Ctx, 1, lowWins ? 10 : 40, day, userId: 1);
        SeedSub(db.Ctx, 1, lowWins ? 10 : 40, day, userId: 2);
        SeedSub(db.Ctx, 1, 20, day, userId: 3);
        SeedSub(db.Ctx, 1, 30, day, userId: 4);

        var repo = new EfLeagueStandingsRepository(db.Ctx);
        var rows = await repo.GetLeaderboardAsync(new LeagueGameDay(1, 1, day), DailyChallenges.Services.Ranking.RankingStrategyFactory.GetStrategy(mode));

        Assert.Equal(new[] { 1, 1, 3, 4 }, rows.Select(r => r.Rank));
        Assert.Equal(lowWins ? new[] { 10.0, 10, 20, 30 } : new[] { 40.0, 40, 30, 20 }, rows.Select(r => r.ScoreValue));
        Assert.Equal(lowWins ? new[] { 3, 4 } : new[] { 4, 3 }, rows.Skip(2).Select(r => r.UserId));
        Assert.Equal(4, rows.Select(r => r.UserId).Distinct().Count());
    }
}
