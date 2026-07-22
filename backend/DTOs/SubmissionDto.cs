namespace DailyChallenges.DTOs
{
    public class SubmissionDto
    {
        public int Id { get; set; }
        public int GameId { get; set; }
        public int? UserId { get; set; }
        public string Score { get; set; } = string.Empty;
        public double? ScoreValue { get; set; }
        // 1-based rank for scored submissions (computed server-side when applicable)
        public int? Rank { get; set; }
        // true when this submission is the winning submission for its scoring day
        public bool IsDayWinner { get; set; }
        public string? Username { get; set; }
        public string? ScreenshotUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        // server-computed scoring day (YYYY-MM-DD) according to game's reset settings
        public string? ScoringDay { get; set; }
    }
}
