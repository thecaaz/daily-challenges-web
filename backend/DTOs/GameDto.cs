using System.Collections.Generic;

namespace DailyChallenges.DTOs
{
    public class GameDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public List<SubmissionDto>? Submissions { get; set; }
    }
}
