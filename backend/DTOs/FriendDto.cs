namespace DailyChallenges.DTOs;

public sealed record FriendDto
{
    public int UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public int Level { get; init; }
    public long TotalXp { get; init; }
    public int Streak { get; init; }
    public DateTime? LastSubmissionAt { get; init; }
}
