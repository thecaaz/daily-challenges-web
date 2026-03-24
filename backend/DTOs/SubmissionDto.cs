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
        // server-computed scoring day (YYYY-MM-DD) according to game's reset settings
        public string? ScoringDay { get; set; }
    }
}
