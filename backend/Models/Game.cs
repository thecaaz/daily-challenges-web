using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DailyChallenges.Models
{
    public class Game
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        // Relative URL to the image stored in wwwroot
        public string? ImageUrl { get; set; }

        // Navigation property
        public List<Submission> Submissions { get; set; } = new();
    }
}
