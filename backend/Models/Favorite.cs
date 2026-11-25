using System.ComponentModel.DataAnnotations;

namespace DailyChallenges.Models;

public sealed class Favorite
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    public int GameId { get; set; }

    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
    public Game? Game { get; set; }
}
