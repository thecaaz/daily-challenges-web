using System.ComponentModel.DataAnnotations;

namespace DailyChallenges.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;
        public bool IsAdmin { get; set; } = false;
    }
}
