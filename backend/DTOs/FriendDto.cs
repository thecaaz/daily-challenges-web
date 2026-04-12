namespace DailyChallenges.DTOs
{
    public class FriendDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public int Level { get; set; }
        public long TotalXp { get; set; }
        public int Streak { get; set; }
        public DateTime? LastSubmissionAt { get; set; }
    }
}
