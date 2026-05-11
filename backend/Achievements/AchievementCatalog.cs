namespace DailyChallenges.Achievements
{
    public class AchievementDefinition
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        /// <summary>Slug used by the frontend to map to an icon or emoji.</summary>
        public string IconKey { get; init; } = string.Empty;
    }

    public enum AchievementTrigger
    {
        Submission,
        LevelUp,
        DayWin,
        FriendAccepted,
    }

    public static class AchievementCatalog
    {
        public static readonly IReadOnlyList<AchievementDefinition> All = new[]
        {
            // ── Submissions ─────────────────────────────────────────────────────
            new AchievementDefinition { Id = "submission_first",  Name = "First Step",       Description = "Submit your first score.",               IconKey = "submit_first"  },
            new AchievementDefinition { Id = "submission_50",     Name = "Committed",         Description = "Submit 50 scores.",                      IconKey = "submit_50"     },
            new AchievementDefinition { Id = "submission_250",    Name = "Dedicated",         Description = "Submit 250 scores.",                     IconKey = "submit_250"    },

            // ── Streaks ─────────────────────────────────────────────────────────
            new AchievementDefinition { Id = "streak_7",          Name = "Week Warrior",      Description = "Maintain a 7-day submission streak.",    IconKey = "streak_7"      },
            new AchievementDefinition { Id = "streak_30",         Name = "Monthly Habit",     Description = "Maintain a 30-day submission streak.",   IconKey = "streak_30"     },
            new AchievementDefinition { Id = "streak_100",        Name = "Centurion",         Description = "Maintain a 100-day submission streak.",  IconKey = "streak_100"    },

            // ── Daily wins ──────────────────────────────────────────────────────
            new AchievementDefinition { Id = "win_1",             Name = "Winner",            Description = "Win your first scoring day.",            IconKey = "win_1"         },
            new AchievementDefinition { Id = "win_10",            Name = "Champion",          Description = "Win 10 scoring days.",                   IconKey = "win_10"        },
            new AchievementDefinition { Id = "win_50",            Name = "Unstoppable",       Description = "Win 50 scoring days.",                   IconKey = "win_50"        },

            // ── Levels ──────────────────────────────────────────────────────────
            new AchievementDefinition { Id = "level_5",           Name = "Rising Up",         Description = "Reach level 5.",                         IconKey = "level_5"       },
            new AchievementDefinition { Id = "level_10",          Name = "Veteran",           Description = "Reach level 10.",                        IconKey = "level_10"      },
            new AchievementDefinition { Id = "level_25",          Name = "Legend",            Description = "Reach level 25.",                        IconKey = "level_25"      },

            // ── Social ──────────────────────────────────────────────────────────
            new AchievementDefinition { Id = "first_friend",      Name = "Better Together",   Description = "Add your first friend.",                 IconKey = "first_friend"  },
        };

        private static readonly Dictionary<string, AchievementDefinition> _byId =
            All.ToDictionary(a => a.Id, a => a);

        public static AchievementDefinition? GetById(string id) =>
            _byId.TryGetValue(id, out var def) ? def : null;
    }
}
