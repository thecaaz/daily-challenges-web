namespace DailyChallenges.DTOs
{
    public class UserProfileDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public long TotalXp { get; set; }
        public int Level { get; set; } = 1;
        public long XpIntoLevel { get; set; }
        public long XpToNextLevel { get; set; }
        public int Streak { get; set; }
        public DateTime? LastSubmissionAt { get; set; }
        public List<UserGameStatDto> TopGames { get; set; } = new();
    }
}
