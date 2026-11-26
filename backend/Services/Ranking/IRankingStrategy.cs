using DailyChallenges.Models;

namespace DailyChallenges.Services.Ranking;

/// <summary>
/// Orders submissions so that the best-ranked one comes first (index 0).
/// Ties are broken by CreatedAt ascending (earliest submission wins).
/// </summary>
public interface IRankingStrategy
{
    /// <summary>Applies the ordering to a submission query.</summary>
    IOrderedQueryable<Submission> ApplyOrdering(IQueryable<Submission> query);

    /// <summary>Applies the same ordering to rows already in memory.</summary>
    IOrderedEnumerable<T> OrderBestFirst<T>(IEnumerable<T> rows) where T : IRankable;
}
