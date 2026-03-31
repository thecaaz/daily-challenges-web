namespace DailyChallenges.DTOs
{
    public class TodaySubmittersDto
    {
        public int Count { get; set; }
        public List<string> Usernames { get; set; } = new();
    }
}
