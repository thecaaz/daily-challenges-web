using System.Security.Claims;
using DailyChallenges.DTOs;

namespace DailyChallenges.Services.Contracts;

public interface IUserProfileService
{
    /// <summary>
    /// Current-scoring-day entries are left out (ScoresHidden = true) unless <paramref name="viewer"/> is the owner or an admin.
    /// </summary>
    Task<UserProfileDto?> GetProfileAsync(int userId, int topGames, ClaimsPrincipal? viewer, CancellationToken ct);
}
