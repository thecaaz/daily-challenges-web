using DailyChallenges.DTOs;

namespace DailyChallenges.Services.Contracts
{
    public interface IScoreMaintenanceService
    {
        /// <summary>
        /// Re-parses every Submission.Score with the current ScoreParser and reports the differences;
        /// writes the new ScoreValues when <paramref name="dryRun"/> is false. Finalized days are not re-finalized.
        /// </summary>
        Task<ReparseScoresReportDto> ReparseScoresAsync(bool dryRun);
    }
}
