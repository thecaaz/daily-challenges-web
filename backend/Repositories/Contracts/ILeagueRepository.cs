using DailyChallenges.Models;

namespace DailyChallenges.Repositories.Contracts;

/// <summary>Leagues and their memberships.</summary>
public interface ILeagueRepository
{
    Task<League?> GetByIdAsync(int leagueId, CancellationToken ct);

    /// <summary>
    /// Loads the league with owner, members (with users) and invitations, untracked. Read-only: the graph
    /// holds the same user twice (Owner and Members[i].User), so never pass it to Update/Remove; use
    /// <see cref="RenameAsync"/> / <see cref="DeleteAsync"/> instead.
    /// </summary>
    Task<League?> GetByIdWithMembersAsync(int leagueId, CancellationToken ct);

    Task<List<League>> GetByUserAsync(int userId, CancellationToken ct);

    /// <summary>Inserts the league together with its initial <see cref="League.Members"/> in one SaveChanges (one transaction).</summary>
    Task<League> CreateAsync(League league, CancellationToken ct);

    /// <summary>Sets the league's name. Returns false when the league does not exist.</summary>
    Task<bool> RenameAsync(int leagueId, string name, CancellationToken ct);

    /// <summary>Deletes the league; members and invitations cascade in the database. Returns false when it does not exist.</summary>
    Task<bool> DeleteAsync(int leagueId, CancellationToken ct);

    Task<LeagueMember?> GetMemberAsync(int leagueId, int userId, CancellationToken ct);

    Task RemoveMemberAsync(LeagueMember member, CancellationToken ct);
}
