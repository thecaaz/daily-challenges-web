namespace DailyChallenges.DTOs
{
    public class XpEventDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int? SubmissionId { get; set; }
        public int? GameId { get; set; }
        /// <summary>Date-only "yyyy-MM-dd".</summary>
        public string? ScoringDay { get; set; }
        public int Amount { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
