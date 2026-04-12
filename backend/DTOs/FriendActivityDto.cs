namespace DailyChallenges.DTOs
{
    public class FriendSubmissionItem
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
    }

    public class FriendActivityDto
    {
        public int GameId { get; set; }
        public string GameName { get; set; } = string.Empty;
        public string? GameImageUrl { get; set; }
        public string? GameUrl { get; set; }
        public bool IsFavorite { get; set; }
        public bool HasSubmittedForLatest { get; set; }
        public List<FriendSubmissionItem> RecentSubmissions { get; set; } = new();
    }
}
