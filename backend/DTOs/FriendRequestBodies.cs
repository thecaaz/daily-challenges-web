namespace DailyChallenges.DTOs;

public sealed record SendFriendRequestDto
{
    public int TargetUserId { get; init; }
}

public sealed record SendFriendRequestByUsernameDto
{
    public string Username { get; init; } = string.Empty;
}

public sealed record RelationshipStatusDto
{
    public string Status { get; init; } = string.Empty;
}
