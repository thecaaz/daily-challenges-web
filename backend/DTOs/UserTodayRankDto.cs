namespace DailyChallenges.DTOs
{
    public class UserTodayRankDto
    {
        public int GameId { get; set; }
        public string GameName { get; set; } = string.Empty;
        public string? GameImageUrl { get; set; }
        public string Score { get; set; } = string.Empty;
        /// <summary>1-based position among today's submissions for this game.</summary>
        public int Rank { get; set; }
        public int TotalSubmissions { get; set; }
    }
}
