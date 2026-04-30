using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories;

namespace DailyChallenges.IntegrationTests;

public class EfLeagueRepositoryGameSummariesTests
{
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
            Username = userId.HasValue ? $"user{userId}" : "anon",
            CreatedAt = scoringDay.Date
        };
        ctx.Submissions.Add(sub);
        ctx.SaveChanges();
        return sub;
    }

    [Fact]
    public async Task GetLeagueGameSummaries_ReturnsAggregatesAndUserRank()
    {
        await using var db = new TestDb();

        // users
        SeedUser(db.Ctx, 1);
        SeedUser(db.Ctx, 2);

        // league + members
        db.Ctx.Leagues.Add(new League { Id = 1, OwnerId = 1, Name = "L1", CreatedAt = DateTime.UtcNow });
        db.Ctx.SaveChanges();
        db.Ctx.LeagueMembers.Add(new LeagueMember { LeagueId = 1, UserId = 1, Role = LeagueRole.Owner, JoinedAt = DateTime.UtcNow });
        db.Ctx.LeagueMembers.Add(new LeagueMember { LeagueId = 1, UserId = 2, Role = LeagueRole.Member, JoinedAt = DateTime.UtcNow });
        db.Ctx.SaveChanges();

        // game
        SeedGame(db.Ctx, 1);

        var day1 = DateTime.UtcNow.Date.AddDays(-1);
        var day2 = DateTime.UtcNow.Date;

        // submissions: user1=100 (day1), user2=200 (day1), user1=300 (day2)
        SeedSub(db.Ctx, 1, 100, day1, userId: 1);
        SeedSub(db.Ctx, 1, 200, day1, userId: 2);
        SeedSub(db.Ctx, 1, 300, day2, userId: 1);

        var repo = new EfLeagueRepository(db.Ctx);

        var (items, total) = await repo.GetLeagueGameSummariesAsync(1, requestingUserId: 1, days: 2, page: 1, pageSize: 20);

        Assert.Equal(1, total);
        Assert.Single(items);
        var res = items[0];

        Assert.Equal(1, res.GameId);
        Assert.Equal(3, res.PlayCount);
        Assert.Equal(300, res.TopScoreValue);
        Assert.Equal(300, res.MyBestScoreValue);
        Assert.Equal(1, res.MyRank); // user1 has the best score (300)

        // recent plays oldest->newest: day1=2, day2=1
        Assert.Equal(new List<int> { 2, 1 }, res.RecentPlays);
    }
}
