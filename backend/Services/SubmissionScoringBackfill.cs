using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using DailyChallenges.Data;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Services
{
    /// <summary>
    /// Small backfill helper to populate nullable Submission.ScoringDay in batches.
    /// Intended to be run as a one-off admin/backfill job or invoked from a background worker.
    /// </summary>
    public class SubmissionScoringBackfill
    {
        private readonly AppDbContext _db;

        public SubmissionScoringBackfill(AppDbContext db)
        {
            _db = db;
        }

        public async Task<int> BackfillBatchAsync(int batchSize = 500, CancellationToken cancellationToken = default)
        {
            var batch = await _db.Submissions
                .Where(s => s.ScoringDay == null)
                .OrderBy(s => s.Id)
                .Take(batchSize)
                .AsNoTracking()
                .Select(s => new { s.Id, s.GameId, s.CreatedAt })
                .ToListAsync(cancellationToken);

            if (!batch.Any()) return 0;

            var gameIds = batch.Select(b => b.GameId).Distinct().ToList();
            var games = await _db.Games.Where(g => gameIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, cancellationToken);

            var updates = new List<SubmissionUpdate>();
            foreach (var item in batch)
            {
                games.TryGetValue(item.GameId, out var game);
                var resetTime = game?.ResetTime ?? TimeSpan.Zero;
                var resetTz = game?.ResetTimezoneId ?? "UTC";
                var scoringDay = ScoringDayHelper.GetScoringDay(item.CreatedAt, resetTime, resetTz);
                updates.Add(new SubmissionUpdate { Id = item.Id, ScoringDay = scoringDay });
            }

            // Apply updates in a single transaction
            foreach (var u in updates)
            {
                var existing = await _db.Submissions.FindAsync(new object[] { u.Id }, cancellationToken);
                if (existing == null) continue;
                existing.ScoringDay = u.ScoringDay;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return updates.Count;
        }

        private class SubmissionUpdate
        {
            public int Id { get; set; }
            public DateTime ScoringDay { get; set; }
        }
    }
}
