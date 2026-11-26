namespace DailyChallenges.DTOs;

public sealed record NotificationDto
{
    public int Id { get; init; }
    public string Message { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public int? GameId { get; init; }
    public string? ScoringDay { get; init; }
    public int? Rank { get; init; }
    /// <summary>The user who caused the notification (friend/league actions); null for system notifications.</summary>
    public int? ActorUserId { get; init; }
    public bool IsRead { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record NotificationPageDto
{
    public List<NotificationDto> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int UnreadCount { get; init; }
}
