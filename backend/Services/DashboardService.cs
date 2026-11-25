using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Contracts;
using DailyChallenges.Services.Dashboard;

namespace DailyChallenges.Services;

public sealed class DashboardService(
    DashboardTodaySnapshotLoader todaySnapshots,
    IFriendRepository friends,
    DashboardFriendActivityBuilder friendActivity,
    IXpEventRepository xpEvents,
    ISubmissionRepository submissions,
    LevelCalculator levels) : IDashboardService
{
    /// <summary>XP event types that count towards "XP earned today" (the streak bonus is part of "submission").</summary>
    private static readonly string[] TodayXpEventTypes =
        { XpEventTypes.Submission, XpEventTypes.DayWin, XpEventTypes.SubmissionRevoked };

    /// <summary>
    /// Each XP event counts only on its own game's current scoring day. The stat-card totals are real
    /// counts, not the length of the capped recent-games list.
    /// </summary>
    public async Task<DashboardDataDto> GetDashboardDataAsync(int userId)
    {
        var today = await todaySnapshots.LoadAsync(userId);
        var friendUsers = await friends.GetFriendsAsync(userId);
        var (activeGameCount, submissionCountToday) = await submissions.CountOnGameDaysAsync(today.ScoringDayByGame);

        return new DashboardDataDto
        {
            RecentGames = DashboardRecentGames.Build(today),
            FriendActivity = await friendActivity.BuildAsync(friendUsers.Select(u => u.Id).ToHashSet(), today),
            Friends = ToFriendDtos(friendUsers, today.Now),
            XpEarnedToday = await xpEvents.SumAmountByUserAndGameDaysAsync(userId, today.ScoringDayByGame, TodayXpEventTypes),
            UserTodayRanks = DashboardTodayRanks.Compute(today),
            ActiveGameCount = activeGameCount,
            SubmissionCountToday = submissionCountToday
        };
    }

    private List<FriendDto> ToFriendDtos(List<User> friendUsers, DateTime now) =>
        friendUsers
            .Select(u => DtoMapper.ToFriendDto(u, levels, now))
            .OrderByDescending(f => f.LastSubmissionAt)
            .ToList();
}
