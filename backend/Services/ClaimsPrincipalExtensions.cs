using System.Security.Claims;

namespace DailyChallenges.Services;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The NameIdentifier claim of an authenticated caller as an int; null otherwise.</summary>
    public static int? GetUserId(this ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true) return null;
        return int.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) ? userId : null;
    }

    /// <summary>True for an authenticated caller in the Admin role.</summary>
    public static bool IsAdmin(this ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true && user.IsInRole(AuthRoles.Admin);
}
