using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;

namespace DailyChallenges.Achievements;

/// <summary>Reads the user statistics achievement thresholds are compared with.</summary>
public sealed class AchievementProgressReader(
    ISubmissionRepository submissions,
    IScoringDayResultRepository scoringDayResults,
    IFriendRepository friends)
{
    /// <summary>
    /// The current value of each metric in <paramref name="metrics"/>; each one is read at most once, and
    /// metrics that are not asked for cost no query.
    /// </summary>
    public async Task<IReadOnlyDictionary<AchievementMetric, int>> GetAsync(User user, IEnumerable<AchievementMetric> metrics, CancellationToken ct)
    {
        var values = new Dictionary<AchievementMetric, int>();
        foreach (var metric in metrics.Distinct())
            values[metric] = await GetAsync(user, metric, ct);
        return values;
    }

    private async Task<int> GetAsync(User user, AchievementMetric metric, CancellationToken ct) => metric switch
    {
        AchievementMetric.SubmissionCount => await submissions.CountByUserAsync(user.Id, ct),
        AchievementMetric.Streak => user.Streak,
        AchievementMetric.DayWins => await scoringDayResults.CountWinsByUserAsync(user.Id, ct),
        AchievementMetric.Level => user.Level,
        AchievementMetric.AcceptedFriends => await friends.HasAcceptedFriendAsync(user.Id, ct) ? 1 : 0,
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown achievement metric"),
    };
}
