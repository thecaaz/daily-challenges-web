using DailyChallenges.Data;
using DailyChallenges.DTOs;
using DailyChallenges.Repositories;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Services
{
    /// <summary>
    /// Repairs stored ScoreValues after parser changes (old first-number parsing, the GLOB migration's values).
    /// </summary>
    public class ScoreMaintenanceService : IScoreMaintenanceService
    {
        public const int SampleLimit = 100;

        public const string PastWinnersNote =
            "Only Submission.ScoreValue is rewritten. Finalized days are not re-finalized: past winners, day-win XP and result notifications stay as they are.";

        private readonly ISubmissionRepository _subs;
        private readonly AppDbContext _db;
        private readonly ILogger<ScoreMaintenanceService> _logger;

        public ScoreMaintenanceService(ISubmissionRepository subs, AppDbContext db, ILogger<ScoreMaintenanceService> logger)
        {
            _subs = subs;
            _db = db;
            _logger = logger;
        }

        public async Task<ReparseScoresReportDto> ReparseScoresAsync(bool dryRun)
        {
            var report = new ReparseScoresReportDto { DryRun = dryRun, SampleLimit = SampleLimit, Note = PastWinnersNote };
            var changes = new List<(int Id, double? Value)>();

            await using var tx = dryRun ? null : await _db.Database.BeginTransactionAsync();
            try
            {
                // Read inside the write transaction, so no submission changes between the diff and the update.
                var rows = await _subs.GetScoreRowsAsync();
                report.Total = rows.Count;

                foreach (var (id, score, oldValue) in rows)
                {
                    double? newValue = ScoreParser.TryParseDouble(score, out var v) ? v : null;
                    if (newValue == oldValue) continue;

                    report.Changed++;
                    if (newValue == null) report.BecameNull++;
                    else if (oldValue == null) report.BecameNumeric++;
                    if (report.Samples.Count < SampleLimit)
                        report.Samples.Add(new ReparseScoreSampleDto { Id = id, Score = score, OldValue = oldValue, NewValue = newValue });
                    changes.Add((id, newValue));
                }

                if (tx != null)
                {
                    // One UPDATE per changed row, all in this single transaction (all or nothing).
                    foreach (var (id, value) in changes)
                        await _subs.SetScoreValueAsync(id, value);
                    await tx.CommitAsync();
                    _logger.LogWarning("Re-parsed scores: {Changed} of {Total} ScoreValues rewritten ({BecameNull} now unranked, {BecameNumeric} now ranked)",
                        report.Changed, report.Total, report.BecameNull, report.BecameNumeric);
                }
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync();
                throw;
            }

            return report;
        }
    }
}
