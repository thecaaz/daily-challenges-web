using DailyChallenges.DTOs;
using DailyChallenges.Exceptions;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Services;

public sealed class LeagueService(ILeagueRepository leagues, IUserProfileRepository users, TimeProvider time)
    : ILeagueService
{
    private const int MaxNameLength = 100;

    public async Task<LeagueDto> CreateLeagueAsync(int ownerId, string name)
    {
        var leagueName = RequireValidName(name);
        var owner = await users.GetByIdAsync(ownerId)
            ?? throw new KeyNotFoundException("User not found.");

        var league = await leagues.CreateAsync(NewLeagueOwnedBy(ownerId, leagueName));

        return LeagueDtoMapper.ToDto(league, owner.Username);
    }

    public async Task<LeagueDto> RenameLeagueAsync(int leagueId, int requestingUserId, string newName)
    {
        var leagueName = RequireValidName(newName);
        var league = await leagues.GetExistingWithMembersAsync(leagueId);
        league.RequireOwner(requestingUserId);

        if (!await leagues.RenameAsync(leagueId, leagueName))
            throw new KeyNotFoundException(LeagueAccess.LeagueNotFound);

        league.Name = leagueName;
        return LeagueDtoMapper.ToDto(league);
    }

    public async Task DeleteLeagueAsync(int leagueId, int requestingUserId)
    {
        var league = await leagues.GetExistingAsync(leagueId);
        league.RequireOwner(requestingUserId);

        if (!await leagues.DeleteAsync(leagueId))
            throw new KeyNotFoundException(LeagueAccess.LeagueNotFound);
    }

    public async Task<List<LeagueDto>> GetMyLeaguesAsync(int userId)
    {
        var myLeagues = await leagues.GetByUserAsync(userId);
        return myLeagues.Select(LeagueDtoMapper.ToDto).ToList();
    }

    public async Task<LeagueDetailDto> GetLeagueDetailAsync(int leagueId, int requestingUserId)
    {
        var league = await leagues.GetExistingWithMembersAsync(leagueId);
        league.RequireMember(requestingUserId);

        var nowUtc = time.GetUtcNow().UtcDateTime;
        return LeagueDtoMapper.ToDetailDto(league, PendingInvitationsVisibleTo(league, requestingUserId), nowUtc);
    }

    public async Task LeaveLeagueAsync(int leagueId, int userId)
    {
        var league = await leagues.GetExistingAsync(leagueId);
        if (league.OwnerId == userId)
            throw new ValidationException("The owner cannot leave the league. Transfer ownership or delete the league.");

        var member = await leagues.GetMemberAsync(leagueId, userId)
            ?? throw new ForbiddenException(LeagueAccess.NotAMember);

        await leagues.RemoveMemberAsync(member);
    }

    public async Task KickMemberAsync(int leagueId, int requestingUserId, int targetUserId)
    {
        var league = await leagues.GetExistingAsync(leagueId);
        league.RequireOwner(requestingUserId);
        if (targetUserId == requestingUserId)
            throw new ValidationException("The owner cannot kick themselves.");

        var member = await leagues.GetMemberAsync(leagueId, targetUserId)
            ?? throw new NotFoundException("The target user is not a member of this league.");

        await leagues.RemoveMemberAsync(member);
    }

    private static string RequireValidName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > MaxNameLength)
            throw new ArgumentException($"League name must be between 1 and {MaxNameLength} characters.");
        return trimmed;
    }

    /// <summary>The owner's membership is part of the new league, so both are inserted in one transaction.</summary>
    private League NewLeagueOwnedBy(int ownerId, string name)
    {
        var nowUtc = time.GetUtcNow().UtcDateTime;
        return new League
        {
            Name = name,
            OwnerId = ownerId,
            CreatedAt = nowUtc,
            Members = [new LeagueMember { UserId = ownerId, Role = LeagueRole.Owner, JoinedAt = nowUtc }]
        };
    }

    /// <summary>Only the owner sees pending invitations: they expose invite tokens and invitees.</summary>
    private static IEnumerable<LeagueInvitation> PendingInvitationsVisibleTo(League league, int userId) =>
        league.OwnerId == userId
            ? league.Invitations.Where(i => i.Status == LeagueInvitationStatus.Pending)
            : [];
}
