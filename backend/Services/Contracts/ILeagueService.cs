using DailyChallenges.DTOs;

namespace DailyChallenges.Services.Contracts
{
    public interface ILeagueService
    {
        Task<LeagueDto> CreateLeagueAsync(int ownerId, string name);
        Task<LeagueDto> RenameLeagueAsync(int leagueId, int requestingUserId, string newName);
        Task DeleteLeagueAsync(int leagueId, int requestingUserId);

        Task<List<LeagueDto>> GetMyLeaguesAsync(int userId);
        Task<LeagueDetailDto> GetLeagueDetailAsync(int leagueId, int requestingUserId);

        Task<LeagueInvitationDto> InviteByUsernameAsync(int leagueId, int inviterId, string username);
        Task<LeagueInvitationDto> CreateInviteLinkAsync(int leagueId, int inviterId);
        Task<LeagueDetailDto> AcceptInvitationAsync(int invitationId, int userId);
        Task<LeagueDetailDto> JoinByTokenAsync(string token, int userId);
        Task DeclineInvitationAsync(int invitationId, int userId);
        Task CancelInvitationAsync(int invitationId, int requestingUserId);

        Task<List<LeagueInvitationDto>> GetMyPendingInvitationsAsync(int userId);

        Task LeaveLeagueAsync(int leagueId, int userId);
        Task KickMemberAsync(int leagueId, int requestingUserId, int targetUserId);

        Task<LeagueLeaderboardDto> GetLeaderboardAsync(int leagueId, int gameId, DateTime scoringDay, int requestingUserId);
    }
}
