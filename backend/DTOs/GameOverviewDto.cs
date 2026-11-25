namespace DailyChallenges.DTOs;

public sealed record OverviewErrorDto
{
    public string? Part { get; init; }
    public string? Message { get; init; }
}

public sealed record GameOverviewDto
{
    public GameDto? Game { get; set; }
    public List<string>? AvailableDates { get; set; }
    public bool HasSubmittedForLatest { get; set; }
    public List<OverviewErrorDto>? Errors { get; set; }
}
