using DailyChallenges.Exceptions;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;

namespace DailyChallenges.Services;

/// <summary>
/// Admin user delete. Every dependent row is handled inside one transaction before the user row goes
/// (see <see cref="IUserDeletionRepository"/>); submissions stay but are anonymized.
/// </summary>
public sealed class AdminUserDeletionService(
    ITransactionRunner transactions,
    IUserDeletionRepository accounts,
    ILogger<AdminUserDeletionService> logger) : IAdminUserDeletionService
{
    private sealed record DeletionSummary(int FriendRequests, int Invitations, int OwnedLeagues);

    public async Task DeleteUserAsync(int userId, int requestingAdminId, CancellationToken ct)
    {
        if (userId == requestingAdminId) throw new ValidationException("Cannot delete your own account");

        var summary = await transactions.RunAsync(() => DeleteWithDependentsAsync(userId, ct), ct);

        logger.LogInformation(
            "Admin {AdminId} deleted user {UserId} ({FriendRequests} friend requests, {Invitations} invitations, {Leagues} owned leagues handled)",
            requestingAdminId, userId, summary.FriendRequests, summary.Invitations, summary.OwnedLeagues);
    }

    private async Task<DeletionSummary> DeleteWithDependentsAsync(int userId, CancellationToken ct)
    {
        var user = await GetDeletableUserAsync(userId, ct);
        var friendRequests = await accounts.DeleteFriendRequestsAsync(userId, ct);
        var invitations = await accounts.DeleteLeagueInvitationsAsync(userId, ct);
        var ownedLeagues = await HandOverOwnedLeaguesAsync(userId, ct);
        await accounts.AnonymizeSubmissionsAsync(userId, ct);

        user.TokenVersion++;
        await accounts.RemoveUserAsync(user, ct);
        return new DeletionSummary(friendRequests, invitations, ownedLeagues);
    }

    private async Task<User> GetDeletableUserAsync(int userId, CancellationToken ct)
    {
        var user = await accounts.GetUserAsync(userId, ct) ?? throw new NotFoundException($"User {userId} not found");
        if (user.IsAdmin) throw new ForbiddenException("Cannot delete an admin account");
        return user;
    }

    /// <summary>Returns the number of leagues the user owned.</summary>
    private async Task<int> HandOverOwnedLeaguesAsync(int userId, CancellationToken ct)
    {
        var ownedLeagues = await accounts.GetOwnedLeaguesAsync(userId, ct);
        foreach (var league in ownedLeagues)
            await HandOverLeagueAsync(league, userId, ct);
        return ownedLeagues.Count;
    }

    /// <summary>The longest-standing other member becomes owner; a league nobody else is in is deleted.</summary>
    private async Task HandOverLeagueAsync(League league, int userId, CancellationToken ct)
    {
        var heir = await accounts.GetLongestStandingOtherMemberAsync(league.Id, userId, ct);
        if (heir == null)
        {
            accounts.RemoveLeague(league);
            logger.LogInformation("League {LeagueId} deleted with its only member, user {UserId}", league.Id, userId);
            return;
        }

        league.OwnerId = heir.UserId;
        heir.Role = LeagueRole.Owner;
        logger.LogInformation("League {LeagueId} transferred from deleted user {UserId} to user {NewOwnerId}", league.Id, userId, heir.UserId);
    }
}
