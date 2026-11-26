using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace DailyChallenges.DTOs;

public sealed record SubmissionCreateRequest
{
    [Required]
    public int GameId { get; set; }

    [Required]
    [StringLength(200)]
    public string Score { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Username { get; set; }

    [Required]
    public IFormFile? Screenshot { get; set; }
}
