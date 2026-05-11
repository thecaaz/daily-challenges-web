namespace DailyChallenges.DTOs
{
    public class AchievementDto
    {
        public string AchievementId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconKey { get; set; } = string.Empty;
        /// <summary>Null when the achievement has not been unlocked yet.</summary>
        public DateTime? UnlockedAt { get; set; }
    }
}
