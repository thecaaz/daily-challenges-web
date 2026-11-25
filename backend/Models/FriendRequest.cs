using System.ComponentModel.DataAnnotations;

namespace DailyChallenges.Models;

/// <summary>
/// At most one request row exists per pair of users, in either direction; see
/// <see cref="Data.Configurations.FriendRequestConfiguration"/>.
/// </summary>
public class FriendRequest
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int SenderId { get; set; }
    public User? Sender { get; set; }

    [Required]
    public int ReceiverId { get; set; }
    public User? Receiver { get; set; }

    public FriendRequestStatus Status { get; set; } = FriendRequestStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Last status change (accept or decline). For a Declined request this is the decline time.</summary>
    public DateTime? UpdatedAt { get; set; }
}
