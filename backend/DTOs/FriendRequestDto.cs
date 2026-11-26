namespace DailyChallenges.DTOs;

public sealed record FriendRequestDto
{
    public int Id { get; init; }
    public int SenderId { get; init; }
    public string SenderUsername { get; init; } = string.Empty;
    public int ReceiverId { get; init; }
    public string ReceiverUsername { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
