namespace DailyChallenges.Achievements;

/// <summary>
/// One achievement: unlocked when, on <see cref="Trigger"/>, the user's <see cref="Metric"/> reaches
/// <see cref="Threshold"/>. <see cref="IconKey"/> is the slug the frontend maps to an icon or emoji.
/// </summary>
public sealed record AchievementDefinition(
    string Id,
    string Name,
    string Description,
    string IconKey,
    AchievementTrigger Trigger,
    AchievementMetric Metric,
    int Threshold);

public enum AchievementTrigger
{
    Submission,
    LevelUp,
    DayWin,
    FriendAccepted,
}

/// <summary>The user statistic an achievement threshold is compared with.</summary>
public enum AchievementMetric
{
    /// <summary>All submissions the user ever made.</summary>
    SubmissionCount,
    /// <summary>The stored <c>User.Streak</c> (not the effective, decayed streak).</summary>
    Streak,
    /// <summary>Scoring days the user won.</summary>
    DayWins,
    Level,
    /// <summary>1 once the user has an accepted friend, else 0.</summary>
    AcceptedFriends,
}

public static class AchievementCatalog
{
    public static readonly IReadOnlyList<AchievementDefinition> All = new[]
    {
        new AchievementDefinition("submission_first", "First Step",      "Submit your first score.",              "submit_first", AchievementTrigger.Submission,     AchievementMetric.SubmissionCount, 1),
        new AchievementDefinition("submission_50",    "Committed",       "Submit 50 scores.",                     "submit_50",    AchievementTrigger.Submission,     AchievementMetric.SubmissionCount, 50),
        new AchievementDefinition("submission_250",   "Dedicated",       "Submit 250 scores.",                    "submit_250",   AchievementTrigger.Submission,     AchievementMetric.SubmissionCount, 250),

        new AchievementDefinition("streak_7",         "Week Warrior",    "Maintain a 7-day submission streak.",   "streak_7",     AchievementTrigger.Submission,     AchievementMetric.Streak,          7),
        new AchievementDefinition("streak_30",        "Monthly Habit",   "Maintain a 30-day submission streak.",  "streak_30",    AchievementTrigger.Submission,     AchievementMetric.Streak,          30),
        new AchievementDefinition("streak_100",       "Centurion",       "Maintain a 100-day submission streak.", "streak_100",   AchievementTrigger.Submission,     AchievementMetric.Streak,          100),

        new AchievementDefinition("win_1",            "Winner",          "Win your first scoring day.",           "win_1",        AchievementTrigger.DayWin,         AchievementMetric.DayWins,         1),
        new AchievementDefinition("win_10",           "Champion",        "Win 10 scoring days.",                  "win_10",       AchievementTrigger.DayWin,         AchievementMetric.DayWins,         10),
        new AchievementDefinition("win_50",           "Unstoppable",     "Win 50 scoring days.",                  "win_50",       AchievementTrigger.DayWin,         AchievementMetric.DayWins,         50),

        new AchievementDefinition("level_5",          "Rising Up",       "Reach level 5.",                        "level_5",      AchievementTrigger.LevelUp,        AchievementMetric.Level,           5),
        new AchievementDefinition("level_10",         "Veteran",         "Reach level 10.",                       "level_10",     AchievementTrigger.LevelUp,        AchievementMetric.Level,           10),
        new AchievementDefinition("level_25",         "Legend",          "Reach level 25.",                       "level_25",     AchievementTrigger.LevelUp,        AchievementMetric.Level,           25),

        new AchievementDefinition("first_friend",     "Better Together", "Add your first friend.",                "first_friend", AchievementTrigger.FriendAccepted, AchievementMetric.AcceptedFriends, 1),
    };

    private static readonly Dictionary<string, AchievementDefinition> _byId =
        All.ToDictionary(a => a.Id, a => a);

    public static AchievementDefinition? GetById(string id) =>
        _byId.TryGetValue(id, out var def) ? def : null;
}
