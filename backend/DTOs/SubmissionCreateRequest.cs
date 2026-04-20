using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace DailyChallenges.DTOs
{
    public class SubmissionCreateRequest
    {
        [Required]
        public int GameId { get; set; }

        [Required]
        [StringLength(200)]
        public string Score { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Username { get; set; }

        public IFormFile? Screenshot { get; set; }
    }
}
