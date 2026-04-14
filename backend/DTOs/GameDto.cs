namespace DailyChallenges.DTOs
{
    public class GameDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        // Optional external URL for the game
        public string? Url { get; set; }
        // Optional description shown on the game detail page
        public string? Description { get; set; }
        // URL to fetch the image blob if present: GET /api/games/{id}/image
        public string? ImageUrl { get; set; }
        // Reset time (HH:mm) in UTC for scoring day boundaries
        public string? ResetTime { get; set; }
        // Current scoring day computed on server (YYYY-MM-DD) based on ResetTime (UTC)
        public string? CurrentScoringDay { get; set; }
        // Whether the current authenticated user has submitted for the CurrentScoringDay
        public bool HasSubmittedForLatest { get; set; }
        public List<SubmissionDto>? Submissions { get; set; }
        // How the daily winner is determined: "highest" or "lowest"
        public string RankingMode { get; set; } = "highest";
        // Whether the current authenticated user has favorited this game
        public bool IsFavorite { get; set; } = false;
    }
}
