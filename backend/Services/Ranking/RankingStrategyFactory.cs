using DailyChallenges.Models;

namespace DailyChallenges.Services.Ranking
{
    public static class RankingStrategyFactory
    {
        public static IRankingStrategy GetStrategy(RankingMode mode) => mode switch
        {
            RankingMode.Lowest => new LowestRankingStrategy(),
            _ => new HighestRankingStrategy()
        };
    }
}
