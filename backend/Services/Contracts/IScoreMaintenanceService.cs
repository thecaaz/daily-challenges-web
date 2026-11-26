using DailyChallenges.DTOs;

namespace DailyChallenges.Services.Contracts;

public interface IScoreMaintenanceService
{
    /// <summary>
    /// Re-parses every Submission.Score with the current ScoreParser and reports the differences
    /// without writing anything (a dry run).
    /// </summary>
    Task<ReparseScoresReportDto> PreviewReparseScoresAsync(CancellationToken ct);

    /// <summary>
    /// Re-parses every Submission.Score with the current ScoreParser, writes the changed ScoreValues in one
    /// transaction and reports them. Finalized days are not re-finalized.
    /// </summary>
    Task<ReparseScoresReportDto> ReparseScoresAsync(CancellationToken ct);
}
