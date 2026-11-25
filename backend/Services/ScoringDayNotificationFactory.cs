using DailyChallenges.Models;
using Microsoft.Extensions.Options;

namespace DailyChallenges.Services;

/// <summary>
/// Builds the end-of-day notifications: the winner gets a "day_win" with the win XP, every other
/// registered participant a "day_placement" with their rank. Anonymous submissions keep their place in
/// the ranking but get no notification.
/// </summary>
public sealed class ScoringDayNotificationFactory(IOptions<XpConfig> config, TimeProvider time)
{
    private const int WinningRank = 1;

    public List<Notification> Create(Game game, DateTime day, IReadOnlyList<Submission> rankedSubmissions)
    {
        var now = time.GetUtcNow().UtcDateTime;
        return RegisteredPlacements(rankedSubmissions)
            .Select(placement => new Notification
            {
                UserId = placement.UserId,
                GameId = game.Id,
                ScoringDay = day,
                Message = MessageFor(game, placement.Rank),
                Type = placement.Rank == WinningRank ? NotificationTypes.DayWin : NotificationTypes.DayPlacement,
                Rank = placement.Rank,
                IsRead = false,
                CreatedAt = now
            })
            .ToList();
    }

    private static IEnumerable<Placement> RegisteredPlacements(IReadOnlyList<Submission> rankedSubmissions)
    {
        for (var index = 0; index < rankedSubmissions.Count; index++)
        {
            if (rankedSubmissions[index].UserId is int userId)
                yield return new Placement(userId, index + 1);
        }
    }

    private string MessageFor(Game game, int rank) =>
        rank == WinningRank
            ? $"You won on {game.Name}! +{config.Value.WinXp} XP"
            : $"You placed #{rank} on {game.Name}";

    private sealed record Placement(int UserId, int Rank);
}
