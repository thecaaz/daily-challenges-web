namespace DailyChallenges.DTOs
{
    public class GameActivityDto
    {
        public GameDto Game { get; set; } = new();
        public int TodayCount { get; set; }
        public List<string> TodayUsernames { get; set; } = new();
    }
}
