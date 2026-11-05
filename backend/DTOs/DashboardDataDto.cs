namespace DailyChallenges.DTOs
{
    public class DashboardDataDto
    {
        public List<GameActivityDto> RecentGames { get; set; } = new();
        public List<FriendActivityDto> FriendActivity { get; set; } = new();
        public List<FriendDto> Friends { get; set; } = new();
        public int XpEarnedToday { get; set; }
        public List<UserTodayRankDto> UserTodayRanks { get; set; } = new();
        /// <summary>Games with at least one submission on their current scoring day (all games, not capped like RecentGames).</summary>
        public int ActiveGameCount { get; set; }
        /// <summary>Total submissions on each game's current scoring day, across all games.</summary>
        public int SubmissionCountToday { get; set; }
    }
}
