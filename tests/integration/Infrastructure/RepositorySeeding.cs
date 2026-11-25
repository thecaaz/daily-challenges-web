using DailyChallenges.Data;
using DailyChallenges.Models;

namespace DailyChallenges.IntegrationTests.Infrastructure;

/// <summary>Saves minimal users and games with fixed ids, for repository tests on a <see cref="TestDb"/>.</summary>
internal static class RepositorySeeding
{
    /// <summary>Saves a user named <c>user{id}</c>.</summary>
    public static void SeedUser(this AppDbContext ctx, int id)
    {
        ctx.Users.Add(new User { Id = id, Username = $"user{id}" });
        ctx.SaveChanges();
    }

    /// <summary>Saves a game named <c>Game{id}</c>.</summary>
    public static void SeedGame(this AppDbContext ctx, int id, RankingMode mode = RankingMode.Highest)
    {
        ctx.Games.Add(new Game { Id = id, Name = $"Game{id}", RankingMode = mode });
        ctx.SaveChanges();
    }
}
