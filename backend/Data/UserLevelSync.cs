using DailyChallenges.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DailyChallenges.Data
{
    /// <summary>
    /// Re-derives Users.Level from Users.TotalXp with the configured level curve. Runs at startup after
    /// Migrate(): data migrations (e.g. UniqueDayWinXpEvent, which removes duplicate day-win XP) can change
    /// TotalXp in SQL but cannot evaluate the curve. A no-op when every level is already correct.
    /// </summary>
    public static class UserLevelSync
    {
        public static int Run(AppDbContext db, LevelCalculator levelCalc, ILogger logger)
        {
            var users = db.Users.AsNoTracking().Select(u => new { u.Id, u.TotalXp, u.Level }).ToList();
            var fixedCount = 0;
            foreach (var u in users)
            {
                var level = levelCalc.GetLevelFromTotalXp(u.TotalXp);
                if (level == u.Level) continue;
                var id = u.Id;
                db.Users.Where(x => x.Id == id).ExecuteUpdate(set => set.SetProperty(x => x.Level, level));
                fixedCount++;
            }

            if (fixedCount > 0)
                logger.LogWarning("Re-derived Level from TotalXp for {Count} user(s).", fixedCount);
            return fixedCount;
        }
    }
}
