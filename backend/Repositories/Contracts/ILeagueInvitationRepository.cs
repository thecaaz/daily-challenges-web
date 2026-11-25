using DailyChallenges.Models;

namespace DailyChallenges.Repositories.Contracts;

/// <summary>Outcome of <see cref="ILeagueInvitationRepository.AcceptInvitationAsync"/>.</summary>
public enum AcceptInvitationResult
{
    Accepted,
    /// <summary>The invitation was no longer pending (already accepted, declined or cancelled).</summary>
    NotPending,
    /// <summary>The user is already a member; nothing was changed.</summary>
    AlreadyMember
}

/// <summary>League invitations: direct (by username) and link-based (by token).</summary>
public interface ILeagueInvitationRepository
{
    Task<LeagueInvitation?> GetInvitationByIdAsync(int invitationId);

    Task<LeagueInvitation?> GetPendingInvitationAsync(int leagueId, int inviteeId);

    Task<LeagueInvitation?> GetInvitationByTokenAsync(string token);

    Task<List<LeagueInvitation>> GetPendingInvitationsForUserAsync(int userId);

    /// <summary>When the invitee last declined an invitation to this league, or null if never.</summary>
    Task<DateTime?> GetLastDeclinedAtAsync(int leagueId, int inviteeId);

    /// <summary>
    /// Inserts the invitation. A unique violation (a second pending invite for the same league and invitee,
    /// or a duplicate token) surfaces as a DbUpdateException; the failed insert is detached first.
    /// </summary>
    Task<LeagueInvitation> CreateInvitationAsync(LeagueInvitation invitation);

    /// <summary>
    /// Moves a pending invitation to <paramref name="status"/> (Declined / Cancelled) with a conditional update.
    /// Returns false when it was no longer pending.
    /// </summary>
    Task<bool> TryCloseInvitationAsync(int invitationId, LeagueInvitationStatus status, DateTime nowUtc);

    /// <summary>
    /// In one transaction: claims the invitation (Pending → Accepted, conditional on it still being pending)
    /// and adds <paramref name="userId"/> as a member. Concurrent accepts of the same invitation (or link)
    /// cannot both succeed; when the user is already a member nothing is changed.
    /// </summary>
    Task<AcceptInvitationResult> AcceptInvitationAsync(int invitationId, int userId, DateTime nowUtc);
}
