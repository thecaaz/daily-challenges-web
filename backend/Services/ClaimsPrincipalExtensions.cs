using System.Security.Claims;

namespace DailyChallenges.Services
{
    public static class ClaimsPrincipalExtensions
    {
        public static int? GetUserId(this ClaimsPrincipal? user)
        {
            if (user?.Identity?.IsAuthenticated != true) return null;
            var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(idClaim, out var parsed)) return parsed;
            return null;
        }

        /// <summary>True for an authenticated caller in the Admin role.</summary>
        public static bool IsAdmin(this ClaimsPrincipal? user) =>
            user?.Identity?.IsAuthenticated == true && user.IsInRole("Admin");
    }
}
