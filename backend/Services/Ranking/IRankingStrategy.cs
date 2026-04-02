using DailyChallenges.Models;

namespace DailyChallenges.Services.Ranking
{
    public interface IRankingStrategy
    {
        /// <summary>
        /// Applies score-based ordering to a submission query.
        /// Always ordered so that the best-ranked submission comes first (index 0).
        /// Ties are broken by CreatedAt ascending (earliest submission wins).
        /// </summary>
        IOrderedQueryable<Submission> ApplyOrdering(IQueryable<Submission> query);
    }
}
