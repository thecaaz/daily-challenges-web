using System.ComponentModel.DataAnnotations;

namespace DailyChallenges.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }
        public User? User { get; set; }

        public int? GameId { get; set; }
        public Game? Game { get; set; }

        public DateTime ScoringDay { get; set; }

        [Required]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Discriminator: "day_win", "day_placement", "friend_request", "friend_request_accepted",
        /// "league_invite", "league_joined", "achievement". Rank is only set for day_win / day_placement.
        /// </summary>
        [Required]
        public string Type { get; set; } = string.Empty;

        public int? Rank { get; set; }

        /// <summary>
        /// The user whose action caused the notification (friend request sender/accepter, league inviter/joiner).
        /// Null for system notifications, and set to null when that user is deleted.
        /// </summary>
        public int? ActorUserId { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
