using DailyChallenges.Exceptions;
using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;

namespace DailyChallenges.Services;

/// <summary>Loads leagues that must exist and enforces who may act on them.</summary>
public static class LeagueAccess
{
    public const string LeagueNotFound = "League not found.";
    public const string NotAMember = "You are not a member of this league.";

    public static async Task<League> GetExistingAsync(this ILeagueRepository leagues, int leagueId) =>
        await leagues.GetByIdAsync(leagueId) ?? throw new KeyNotFoundException(LeagueNotFound);

    public static async Task<League> GetExistingWithMembersAsync(this ILeagueRepository leagues, int leagueId) =>
        await leagues.GetByIdWithMembersAsync(leagueId) ?? throw new KeyNotFoundException(LeagueNotFound);

    public static async Task RequireMembershipAsync(this ILeagueRepository leagues, int leagueId, int userId)
    {
        await leagues.GetExistingAsync(leagueId);
        _ = await leagues.GetMemberAsync(leagueId, userId) ?? throw new ForbiddenException(NotAMember);
    }

    public static void RequireOwner(this League league, int userId)
    {
        if (league.OwnerId != userId)
            throw new ForbiddenException("Only the league owner can perform this action.");
    }

    /// <summary>Checks the loaded <see cref="League.Members"/>.</summary>
    public static void RequireMember(this League league, int userId)
    {
        if (!league.Members.Any(m => m.UserId == userId))
            throw new ForbiddenException(NotAMember);
    }
}
