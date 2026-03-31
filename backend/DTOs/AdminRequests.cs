namespace DailyChallenges.DTOs
{
    public record AdminAdjustXpDto(int Delta, string? Reason);
    public record AdminSetPasswordDto(string NewPassword);
}
