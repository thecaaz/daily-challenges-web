using DailyChallenges.DTOs;

namespace DailyChallenges.Services.Contracts
{
    public interface IUserProfileService
    {
        Task<UserProfileDto?> GetProfileAsync(int userId, int topGames = 10);
    }
}
