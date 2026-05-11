using System.ComponentModel.DataAnnotations;

namespace DailyChallenges.Models
{
    public class UserAchievement
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        /// <summary>Matches a key defined in <see cref="DailyChallenges.Achievements.AchievementCatalog"/>.</summary>
        [Required]
        public string AchievementId { get; set; } = string.Empty;

        public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
    }
}
