using DailyChallenges.Models;

namespace DailyChallenges.Services.Ranking
{
    /// <summary>Lower ScoreValue wins (ascending order). Ties broken by earliest CreatedAt.</summary>
    public class LowestRankingStrategy : IRankingStrategy
    {
        public IOrderedQueryable<Submission> ApplyOrdering(IQueryable<Submission> query)
            => query.OrderBy(s => s.ScoreValue).ThenBy(s => s.CreatedAt);
    }
}
