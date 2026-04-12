namespace DailyChallenges.DTOs
{
    public class DashboardDataDto
    {
        public List<GameActivityDto> RecentGames { get; set; } = new();
        public List<FriendActivityDto> FriendActivity { get; set; } = new();
        public List<FriendDto> Friends { get; set; } = new();
        public int XpEarnedToday { get; set; }
        public List<UserTodayRankDto> UserTodayRanks { get; set; } = new();
    }
}
