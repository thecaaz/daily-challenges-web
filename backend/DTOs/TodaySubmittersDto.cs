namespace DailyChallenges.DTOs;

public sealed record TodaySubmittersDto
{
    public int Count { get; init; }
    public List<string> Usernames { get; init; } = new();
}
