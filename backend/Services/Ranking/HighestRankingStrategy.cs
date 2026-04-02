using DailyChallenges.Models;

namespace DailyChallenges.Services.Ranking
{
    /// <summary>Higher ScoreValue wins (descending order). Ties broken by earliest CreatedAt.</summary>
    public class HighestRankingStrategy : IRankingStrategy
    {
        public IOrderedQueryable<Submission> ApplyOrdering(IQueryable<Submission> query)
            => query.OrderByDescending(s => s.ScoreValue).ThenBy(s => s.CreatedAt);
    }
}
