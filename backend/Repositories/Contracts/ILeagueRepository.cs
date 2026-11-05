using DailyChallenges.Models;
using DailyChallenges.DTOs;
using DailyChallenges.Services.Ranking;

namespace DailyChallenges.Repositories.Contracts
{
    /// <summary>Outcome of <see cref="ILeagueRepository.AcceptInvitationAsync"/>.</summary>
    public enum AcceptInvitationResult
    {
        Accepted,
        /// <summary>The invitation was no longer pending (already accepted, declined or cancelled).</summary>
        NotPending,
        /// <summary>The user is already a member; nothing was changed.</summary>
        AlreadyMember
    }

    public interface ILeagueRepository
    {
        // ── Leagues ───────────────────────────────────────────────────────────
        Task<League?> GetByIdAsync(int leagueId);

        /// <summary>
        /// Loads the league with owner, members (with users) and invitations, untracked. Read-only: the graph
        /// holds the same user twice (Owner and Members[i].User), so never pass it to Update/Remove; use
        /// <see cref="RenameAsync"/> / <see cref="DeleteAsync"/> instead.
        /// </summary>
        Task<League?> GetByIdWithMembersAsync(int leagueId);
        Task<List<League>> GetByUserAsync(int userId);

        /// <summary>Inserts the league together with its initial <see cref="League.Members"/> in one SaveChanges (one transaction).</summary>
        Task<League> CreateAsync(League league);

        /// <summary>Sets the league's name. Returns false when the league does not exist.</summary>
        Task<bool> RenameAsync(int leagueId, string name);

        /// <summary>Deletes the league; members and invitations cascade in the database. Returns false when it does not exist.</summary>
        Task<bool> DeleteAsync(int leagueId);

        // ── Members ───────────────────────────────────────────────────────────
        Task<LeagueMember?> GetMemberAsync(int leagueId, int userId);
        Task RemoveMemberAsync(LeagueMember member);

        // ── Invitations ───────────────────────────────────────────────────────
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

        // ── Leaderboard ───────────────────────────────────────────────────────
        /// <summary>
        /// Returns the best submission per member for the given game and scoring day,
        /// ordered by the provided ranking strategy. Only members with a ScoreValue are included.
        /// </summary>
        Task<List<(int UserId, string Username, int Rank, string Score, double ScoreValue, int SubmissionId, bool HasScreenshot)>> GetLeaderboardAsync(
            int leagueId, int gameId, DateTime scoringDay, IRankingStrategy strategy);

        /// <summary>
        /// Returns a compact summary for each game played by league members in the
        /// given recent window (days). Includes play counts, top score, the
        /// requesting user's best score and rank, and a small recent-play histogram.
        /// </summary>
        Task<(List<LeagueGameSummaryDto> Items, int TotalCount)> GetLeagueGameSummariesAsync(int leagueId, int requestingUserId, int days = 7, int page = 1, int pageSize = 20);
    }
}
