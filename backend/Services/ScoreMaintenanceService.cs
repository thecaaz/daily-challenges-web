using DailyChallenges.DTOs;
using DailyChallenges.Repositories;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Services;

/// <summary>
/// Repairs stored ScoreValues after parser changes (old first-number parsing, the GLOB migration's values).
/// </summary>
public sealed class ScoreMaintenanceService(
    ISubmissionRepository submissions,
    ITransactionRunner transactions,
    ILogger<ScoreMaintenanceService> logger) : IScoreMaintenanceService
{
    public const int SampleLimit = 100;

    public const string PastWinnersNote =
        "Only Submission.ScoreValue is rewritten. Finalized days are not re-finalized: past winners, day-win XP and result notifications stay as they are.";

    private sealed record ScoreValueChange(int Id, string Score, double? OldValue, double? NewValue);

    private sealed record ScoreDiff(int Total, List<ScoreValueChange> Changes);

    public async Task<ReparseScoresReportDto> PreviewReparseScoresAsync(CancellationToken ct) =>
        BuildReport(await DiffStoredScoresAsync(), dryRun: true);

    public async Task<ReparseScoresReportDto> ReparseScoresAsync(CancellationToken ct)
    {
        var report = await transactions.RunAsync(RewriteChangedScoresAsync);
        logger.LogWarning("Re-parsed scores: {Changed} of {Total} ScoreValues rewritten ({BecameNull} now unranked, {BecameNumeric} now ranked)",
            report.Changed, report.Total, report.BecameNull, report.BecameNumeric);
        return report;
    }

    /// <summary>
    /// Diffs inside the write transaction, so no submission changes between the diff and the update.
    /// One UPDATE per changed row, all or nothing.
    /// </summary>
    private async Task<ReparseScoresReportDto> RewriteChangedScoresAsync()
    {
        var diff = await DiffStoredScoresAsync();
        foreach (var change in diff.Changes)
            await submissions.SetScoreValueAsync(change.Id, change.NewValue);
        return BuildReport(diff, dryRun: false);
    }

    private async Task<ScoreDiff> DiffStoredScoresAsync()
    {
        var rows = await submissions.GetScoreRowsAsync();
        var changes = rows
            .Select(row => new ScoreValueChange(row.Id, row.Score, row.ScoreValue, ParseScoreValue(row.Score)))
            .Where(change => change.NewValue != change.OldValue)
            .ToList();
        return new ScoreDiff(rows.Count, changes);
    }

    private static double? ParseScoreValue(string score) =>
        ScoreParser.TryParseDouble(score, out var value) ? value : null;

    private static ReparseScoresReportDto BuildReport(ScoreDiff diff, bool dryRun) => new()
    {
        DryRun = dryRun,
        SampleLimit = SampleLimit,
        Note = PastWinnersNote,
        Total = diff.Total,
        Changed = diff.Changes.Count,
        BecameNull = diff.Changes.Count(change => change.NewValue == null),
        BecameNumeric = diff.Changes.Count(change => change.NewValue != null && change.OldValue == null),
        Samples = diff.Changes
            .Take(SampleLimit)
            .Select(change => new ReparseScoreSampleDto { Id = change.Id, Score = change.Score, OldValue = change.OldValue, NewValue = change.NewValue })
            .ToList(),
    };
}
