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

        /// <summary>UTC reset time, "HH:mm" (00:00–23:59). The client converts from local time itself.</summary>
        [StringLength(10)]
        public string? ResetTime { get; set; }

        /// <summary>Absolute http(s) URL. "" clears it; omitted (null) leaves it unchanged.</summary>
        [StringLength(500)]
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        public string? Url { get; set; }

        /// <summary>"" clears it; omitted (null) leaves it unchanged.</summary>
        [StringLength(2000)]
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        public string? Description { get; set; }

        [StringLength(50)]
        public string? RankingMode { get; set; }
    }
}
