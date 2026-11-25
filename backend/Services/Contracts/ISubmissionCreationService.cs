using DailyChallenges.DTOs;
using System.Security.Claims;

namespace DailyChallenges.Services;

public interface ISubmissionCreationService
{
    /// <summary>
    /// Stores a submission for the game's current scoring day and awards its XP in the same transaction.
    /// Throws ValidationException for an unknown game or a blank score, ConflictException when the user has
    /// already submitted for the day, and ArgumentException when the screenshot is missing or empty.
    /// </summary>
    /// <returns>Tuple of the created SubmissionDto and the XP awarded (0 for anonymous users).</returns>
    Task<(SubmissionDto Dto, int XpGain)> CreateAsync(SubmissionDraftRequest request, ClaimsPrincipal user);
}

/// <summary>A new submission as entered; <see cref="Username"/> is only used for anonymous submitters.</summary>
public sealed record SubmissionDraftRequest(int GameId, string Score, string? Username, IFormFile? Screenshot);
