using DailyChallenges.DTOs;
using System.Security.Claims;

namespace DailyChallenges.Services;

/// <summary>Read paths for submissions. All of them apply the hidden-scores rule of <see cref="IScoreVisibilityService"/>.</summary>
public interface ISubmissionQueryService
{
    /// <summary>
    /// One page of a game's submissions. Without a <see cref="GameSubmissionsPageRequest.ScoringDay"/>, the
    /// current day is left out for callers who have not submitted for it. With the current day, its rows are
    /// listed but masked unless the caller may see them. With a day, scored rows carry their 1-based rank on that day.
    /// </summary>
    Task<SubmissionPageDto> GetByGameAsync(GameSubmissionsPageRequest request, ClaimsPrincipal? user);
    /// <summary>Admin view: every submission of the game, with its stored scoring day.</summary>
    Task<List<SubmissionDto>> GetUnfilteredByGameAsync(int gameId);
    Task<List<string>> GetAvailableDatesAsync(int gameId);
    /// <summary>Current-day screenshots need a valid <paramref name="exp"/>/<paramref name="sig"/> pair (ForbiddenException otherwise).</summary>
    Task<(byte[]? Data, string? ContentType)> GetScreenshotAsync(int id, long? exp, string? sig);
    /// <summary>Masked when the submission is on the current scoring day and the caller (not the owner) may not see it.</summary>
    Task<SubmissionDto?> GetByIdAsync(int id, ClaimsPrincipal? user);
    Task<bool> HasUserSubmittedForLatestAsync(int gameId, ClaimsPrincipal? user);
    /// <summary>Without a day: the previous scoring day's winner. Null when there is none or the day is hidden.</summary>
    Task<SubmissionDto?> GetWinnerAsync(int gameId, DateTime? scoringDay, ClaimsPrincipal? user);
    Task<TodaySubmittersDto> GetTodaySubmittersAsync(int gameId);
}

/// <summary>One page (1-based) of a game's submissions, optionally restricted to one scoring day.</summary>
public sealed record GameSubmissionsPageRequest(int GameId, DateTime? ScoringDay, int Page, int PageSize);
