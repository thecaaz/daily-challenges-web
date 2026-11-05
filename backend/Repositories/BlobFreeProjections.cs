using DailyChallenges.Models;

namespace DailyChallenges.Repositories
{
    /// <summary>
    /// Projections that read every column except the image blob. The results are untracked
    /// copies with an empty/null blob, so use them for reads only (never Update/Remove them).
    /// Whether an image exists is signalled by a non-empty ScreenshotContentType.
    /// Apply these last (after Where/OrderBy/Skip/Take) so ordering still runs on the entity.
    /// </summary>
    public static class BlobFreeProjections
    {
        public static IQueryable<Submission> SelectWithoutBlob(this IQueryable<Submission> query) =>
            query.Select(s => new Submission
            {
                Id = s.Id,
                GameId = s.GameId,
                UserId = s.UserId,
                Score = s.Score,
                ScoreValue = s.ScoreValue,
                Username = s.Username,
                ScreenshotContentType = s.ScreenshotContentType,
                ScoringDay = s.ScoringDay,
                CreatedAt = s.CreatedAt,
                XpAwarded = s.XpAwarded
            });

        public static IQueryable<Game> SelectWithoutBlob(this IQueryable<Game> query) =>
            query.Select(g => new Game
            {
                Id = g.Id,
                Name = g.Name,
                ScreenshotContentType = g.ScreenshotContentType,
                ResetTime = g.ResetTime,
                Url = g.Url,
                Description = g.Description,
                RankingMode = g.RankingMode
            });
    }
}
