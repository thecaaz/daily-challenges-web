namespace DailyChallenges.DTOs
{
    public class UserGameStatDto
    {
        public int GameId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Url { get; set; }
        public int Plays { get; set; }
        public int? HighestScore { get; set; }
        public int? BestSubmissionId { get; set; }
        public DateTime? LastPlayed { get; set; }
        public string? RankingMode { get; set; }
        public List<ScoreHistoryEntryDto> ScoreHistory { get; set; } = new();
    }
}
