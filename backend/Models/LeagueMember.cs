using System.ComponentModel.DataAnnotations;

namespace DailyChallenges.Models
{
    public enum LeagueRole
    {
        Member,
        Owner
    }

    public class LeagueMember
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int LeagueId { get; set; }
        public League? League { get; set; }

        [Required]
        public int UserId { get; set; }
        public User? User { get; set; }

        public LeagueRole Role { get; set; } = LeagueRole.Member;

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
