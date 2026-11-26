using DailyChallenges.Models;

namespace DailyChallenges.Tests;

/// <summary>Minimal users and leagues for service tests that run against mocked repositories.</summary>
internal static class TestEntities
{
    public static User MakeUser(int id, string username = "user") =>
        new() { Id = id, Username = username, Level = 1, Streak = 0, TotalXp = 0 };

    public static League MakeLeague(int id, int ownerId, string name = "My League") =>
        new()
        {
            Id = id,
            Name = name,
            OwnerId = ownerId,
            Owner = MakeUser(ownerId, "owner"),
            CreatedAt = DateTime.UtcNow,
            Members = new List<LeagueMember>
            {
                new() { Id = 1, LeagueId = id, UserId = ownerId, Role = LeagueRole.Owner, User = MakeUser(ownerId, "owner") }
            },
            Invitations = new List<LeagueInvitation>()
        };
}
