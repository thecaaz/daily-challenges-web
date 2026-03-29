namespace DailyChallenges.DTOs
{
    public class SubmissionPageDto
    {
        public List<SubmissionDto> Items { get; set; } = new List<SubmissionDto>();
        public bool HasSubmittedForLatest { get; set; }
        public bool HasMore { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }
}
