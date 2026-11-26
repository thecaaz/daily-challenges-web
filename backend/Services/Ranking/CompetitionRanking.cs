namespace DailyChallenges.Services.Ranking;

/// <summary>Standard competition ranking ("1224"): equal scores share a rank and the next rank skips.</summary>
public static class CompetitionRanking
{
    private const double ScoreTolerance = 1e-9;

    /// <summary>Ranks for scores that are already sorted best first, index for index.</summary>
    public static IReadOnlyList<int> RanksOf(IReadOnlyList<double> scoresBestFirst)
    {
        var ranks = new int[scoresBestFirst.Count];
        for (var i = 0; i < ranks.Length; i++)
            ranks[i] = i > 0 && IsTie(scoresBestFirst[i], scoresBestFirst[i - 1]) ? ranks[i - 1] : i + 1;
        return ranks;
    }

    private static bool IsTie(double score, double previousScore) =>
        Math.Abs(score - previousScore) <= ScoreTolerance;
}
