namespace DailyChallenges.DTOs
{
    public class SubmissionPageDto
    {
        public List<SubmissionDto> Items { get; set; } = new List<SubmissionDto>();
        public bool HasSubmittedForLatest { get; set; }
        /// <summary>True when the requested day is the current scoring day and the caller may not see its scores; items are masked.</summary>
        public bool ScoresHidden { get; set; }
        public bool HasMore { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }
}
