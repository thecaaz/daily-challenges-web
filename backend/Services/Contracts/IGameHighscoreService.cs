using System.Security.Claims;
using DailyChallenges.DTOs;

namespace DailyChallenges.Services;

public interface IGameHighscoreService
{
    /// <summary>The top 50 scored submissions, hiding the current scoring day from callers who may not see it.</summary>
    Task<HighscoreResult> GetHighscoreAsync(int id, ClaimsPrincipal? user);

    /// <summary>The user's own top 50 scored submissions.</summary>
    Task<HighscoreResult> GetPersonalHighscoreAsync(int id, int userId);
}

/// <summary>The ranked <paramref name="Top"/> list and its first entry (null when the list is empty).</summary>
public sealed record HighscoreResult(SubmissionDto? Highscore, List<SubmissionDto> Top);
