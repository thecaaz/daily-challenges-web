namespace DailyChallenges.DTOs;

public sealed record AdminAdjustXpDto(int Delta, string? Reason);
public sealed record AdminSetPasswordDto(string NewPassword);
