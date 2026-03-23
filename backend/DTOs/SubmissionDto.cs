using System;

namespace DailyChallenges.DTOs
{
    public class SubmissionDto
    {
        public int Id { get; set; }
        public int GameId { get; set; }
        public int? UserId { get; set; }
        public string Score { get; set; } = string.Empty;
        public string? Username { get; set; }
        public string? ScreenshotUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
