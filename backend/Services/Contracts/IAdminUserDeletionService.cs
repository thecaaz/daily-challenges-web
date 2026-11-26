namespace DailyChallenges.Services;

public interface IAdminUserDeletionService
{
    /// <summary>
    /// Deletes a non-admin user in one transaction: friend requests and league invitations go, owned leagues
    /// are handed to the longest-standing other member (or deleted when nobody else is in them), submissions
    /// stay anonymized. Throws ValidationException for the caller's own account, NotFoundException for a
    /// missing user and ForbiddenException for an admin account.
    /// </summary>
    Task DeleteUserAsync(int userId, int requestingAdminId, CancellationToken ct);
}
