using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace DailyChallenges.DTOs
{
    public class GameCreateRequest
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        public IFormFile? Image { get; set; }

        [StringLength(10)]
        public string? ResetTime { get; set; }

        [StringLength(500)]
        public string? Url { get; set; }

        [StringLength(2000)]
        public string? Description { get; set; }

        [Range(-1440, 1440)]
        public int? ResetTimezoneOffsetMinutes { get; set; }

        [StringLength(50)]
        public string? RankingMode { get; set; }
    }
}
