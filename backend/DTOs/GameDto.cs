namespace DailyChallenges.DTOs
{
    public class GameDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        // Optional external URL for the game
        public string? Url { get; set; }
        // URL to fetch the image blob if present: GET /api/games/{id}/image
        public string? ImageUrl { get; set; }
        // Reset time (HH:mm) and timezone id for scoring day boundaries
        public string? ResetTime { get; set; }
        public string? ResetTimezoneId { get; set; }
        // Current scoring day computed on server (YYYY-MM-DD) based on ResetTime/ResetTimezoneId
        public string? CurrentScoringDay { get; set; }
        public List<SubmissionDto>? Submissions { get; set; }
    }
}
