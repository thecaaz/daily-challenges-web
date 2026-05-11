using System.ComponentModel.DataAnnotations;

namespace DailyChallenges.Models
{
    public class League
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>The user who created the league and is its owner.</summary>
        [Required]
        public int OwnerId { get; set; }
        public User? Owner { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<LeagueMember> Members { get; set; } = new List<LeagueMember>();
        public ICollection<LeagueInvitation> Invitations { get; set; } = new List<LeagueInvitation>();
    }
}
