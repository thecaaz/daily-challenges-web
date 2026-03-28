using System.Collections.Generic;

namespace DailyChallenges.DTOs
{
    public class OverviewErrorDto
    {
        public string? Part { get; set; }
        public string? Message { get; set; }
    }

    public class GameOverviewDto
    {
        public GameDto? Game { get; set; }
        public List<string>? AvailableDates { get; set; }
        public bool HasSubmittedForLatest { get; set; }
        public List<SubmissionDto>? Top { get; set; }
        public List<OverviewErrorDto>? Errors { get; set; }
    }
}
