using DailyChallenges.Models;

namespace DailyChallenges.Repositories.Contracts;

/// <summary>
/// Data access for deleting a user account together with the rows that depend on it. Callers run the
/// whole sequence in one transaction; the Restrict FKs (friend request receiver, league owner, invitation
/// inviter) stay as a safety net, so every dependent row must be handled before <see cref="RemoveUserAsync"/>.
/// </summary>
public interface IUserDeletionRepository
{
    /// <summary>The tracked user, or null.</summary>
    Task<User?> GetUserAsync(int userId);

    /// <summary>Deletes the user's sent and received friend requests in SQL; returns the deleted row count.</summary>
    Task<int> DeleteFriendRequestsAsync(int userId);

    /// <summary>
    /// Deletes the league invitations the user sent and received in SQL (received ones would otherwise stay
    /// Pending with InviteeId = null); returns the deleted row count.
    /// </summary>
    Task<int> DeleteLeagueInvitationsAsync(int userId);

    /// <summary>The tracked leagues the user owns.</summary>
    Task<List<League>> GetOwnedLeaguesAsync(int userId);

    /// <summary>The tracked member of the league, other than the user, who joined first (ties by Id), or null.</summary>
    Task<LeagueMember?> GetLongestStandingOtherMemberAsync(int leagueId, int userId);

    /// <summary>Marks the league for deletion; it is deleted by <see cref="RemoveUserAsync"/>.</summary>
    void RemoveLeague(League league);

    /// <summary>Clears Username on the user's submissions in SQL. The FK sets their UserId to null on delete.</summary>
    Task AnonymizeSubmissionsAsync(int userId);

    /// <summary>Deletes the user and flushes all pending tracked changes (league hand-overs and removals).</summary>
    Task RemoveUserAsync(User user);
}
