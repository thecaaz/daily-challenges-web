using System.ComponentModel.DataAnnotations;

namespace DailyChallenges.Models
{
    public enum LeagueInvitationStatus
    {
        Pending,
        Accepted,
        Declined,
        Cancelled
    }

    public class LeagueInvitation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int LeagueId { get; set; }
        public League? League { get; set; }

        /// <summary>User who sent the invitation (must be league owner).</summary>
        [Required]
        public int InviterId { get; set; }
        public User? Inviter { get; set; }

        /// <summary>User being invited. Null for token-based invite links.</summary>
        public int? InviteeId { get; set; }
        public User? Invitee { get; set; }

        /// <summary>Opaque token for link-based invites. Null for direct user invitations.</summary>
        [MaxLength(128)]
        public string? Token { get; set; }

        public LeagueInvitationStatus Status { get; set; } = LeagueInvitationStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        /// <summary>When null, the invite never expires.</summary>
        public DateTime? ExpiresAt { get; set; }
    }
}
