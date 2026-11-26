namespace DailyChallenges.DTOs;

public sealed record UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }

    public long TotalXp { get; set; }
    public int Level { get; set; } = 1;
    /// <summary>XP accumulated within the current level.</summary>
    public long XpIntoLevel { get; set; }
    /// <summary>XP still needed to reach the next level.</summary>
    public long XpToNextLevel { get; set; }
    public int Streak { get; set; }
    public DateTime? LastSubmissionAt { get; set; }
}
