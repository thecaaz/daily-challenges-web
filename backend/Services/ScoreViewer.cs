using System.Security.Claims;

namespace DailyChallenges.Services;

/// <summary>Who is asking to see scores: a signed-in user (or nobody), and whether they are an admin.</summary>
public sealed record ScoreViewer(int? UserId, bool IsAdmin)
{
    public static ScoreViewer From(ClaimsPrincipal? user) => new(user.GetUserId(), user.IsAdmin());
}
