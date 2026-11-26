using DailyChallenges.DTOs;

namespace DailyChallenges.Services.Contracts;

/// <summary>League lifecycle (create, rename, delete), league views and roster changes.</summary>
public interface ILeagueService
{
    /// <summary>Creates the league with <paramref name="ownerId"/> as its owner and first member.</summary>
    /// <exception cref="Exceptions.ValidationException">The trimmed name is empty or longer than 100 characters.</exception>
    Task<LeagueDto> CreateLeagueAsync(int ownerId, string name, CancellationToken ct);

    Task<LeagueDto> RenameLeagueAsync(int leagueId, int requestingUserId, string newName, CancellationToken ct);

    Task DeleteLeagueAsync(int leagueId, int requestingUserId, CancellationToken ct);

    Task<List<LeagueDto>> GetMyLeaguesAsync(int userId, CancellationToken ct);

    /// <summary>Members only. Pending invitations are listed for the owner only.</summary>
    Task<LeagueDetailDto> GetLeagueDetailAsync(int leagueId, int requestingUserId, CancellationToken ct);

    /// <summary>Any member except the owner may leave.</summary>
    Task LeaveLeagueAsync(int leagueId, int userId, CancellationToken ct);

    /// <summary>The owner removes another member.</summary>
    Task KickMemberAsync(int leagueId, int requestingUserId, int targetUserId, CancellationToken ct);
}
