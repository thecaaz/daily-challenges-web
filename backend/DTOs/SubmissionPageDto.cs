namespace DailyChallenges.DTOs;

public sealed record SubmissionPageDto
{
    public List<SubmissionDto> Items { get; init; } = new List<SubmissionDto>();
    public bool HasSubmittedForLatest { get; init; }
    /// <summary>True when the requested day is the current scoring day and the caller may not see its scores; items are masked.</summary>
    public bool ScoresHidden { get; init; }
    public bool HasMore => Page < TotalPages;
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
