using DailyChallenges.DTOs;

namespace DailyChallenges.Services;

public interface ISubmissionScoreEditService
{
    /// <summary>
    /// Admin score edit; a blank <paramref name="score"/> leaves the score unchanged. On an already finalized day,
    /// a change of winner re-finalizes the day. Throws NotFoundException when the submission does not exist.
    /// </summary>
    Task<SubmissionDto> UpdateAsync(int id, string? score);
}
