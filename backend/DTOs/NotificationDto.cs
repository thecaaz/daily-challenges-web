namespace DailyChallenges.DTOs
{
    public class NotificationDto
    {
        public int Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int? GameId { get; set; }
        public string? ScoringDay { get; set; }
        public int? Rank { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class NotificationPageDto
    {
        public List<NotificationDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
    }
}
