using DailyChallenges.Models;
using DailyChallenges.Services.Ranking;

namespace DailyChallenges.Repositories.Contracts
{
    public interface ILeagueRepository
    {
        // ── Leagues ───────────────────────────────────────────────────────────
        Task<League?> GetByIdAsync(int leagueId);
        Task<League?> GetByIdWithMembersAsync(int leagueId);
        Task<List<League>> GetByUserAsync(int userId);
        Task<League> CreateAsync(League league);
        Task UpdateAsync(League league);
        Task DeleteAsync(League league);

        // ── Members ───────────────────────────────────────────────────────────
        Task<LeagueMember?> GetMemberAsync(int leagueId, int userId);
        Task<List<LeagueMember>> GetMembersAsync(int leagueId);
        Task<LeagueMember> AddMemberAsync(LeagueMember member);
        Task RemoveMemberAsync(LeagueMember member);

        // ── Invitations ───────────────────────────────────────────────────────
        Task<LeagueInvitation?> GetInvitationByIdAsync(int invitationId);
        Task<LeagueInvitation?> GetPendingInvitationAsync(int leagueId, int inviteeId);
        Task<LeagueInvitation?> GetInvitationByTokenAsync(string token);
        Task<List<LeagueInvitation>> GetPendingInvitationsForUserAsync(int userId);
        Task<List<LeagueInvitation>> GetInvitationsForLeagueAsync(int leagueId);
        Task<LeagueInvitation> CreateInvitationAsync(LeagueInvitation invitation);
        Task UpdateInvitationAsync(LeagueInvitation invitation);

        // ── Leaderboard ───────────────────────────────────────────────────────
        /// <summary>
        /// Returns the best submission per member for the given game and scoring day,
        /// ordered by the provided ranking strategy. Only members with a ScoreValue are included.
        /// </summary>
        Task<List<(int UserId, string Username, int Rank, string Score, int ScoreValue)>> GetLeaderboardAsync(
            int leagueId, int gameId, DateTime scoringDay, IRankingStrategy strategy);
    }
}
