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

        // Stored image as blob
        public byte[]? ScreenshotData { get; set; }
        public string? ScreenshotContentType { get; set; }

        // Navigation property
        public List<Submission> Submissions { get; set; } = new();
    }
}
