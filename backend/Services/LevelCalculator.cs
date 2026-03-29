namespace DailyChallenges.Services
{
    /// <summary>
    /// Pure, stateless utility for converting between total XP and level.
    ///
    /// Curve:  XP required to *complete* level L (i.e. advance from L to L+1):
    ///         xp(L) = Round( LevelBase * L ^ LevelExponent )
    ///
    /// Defaults (configurable in appsettings Xp section):
    ///   LevelBase     = 100
    ///   LevelExponent = 1.5
    ///
    /// Sample thresholds (cumulative XP to *enter* that level):
    ///   Level  1 →       0 XP  (starting level)
    ///   Level  2 →     100 XP
    ///   Level  3 →     383 XP
    ///   Level  4 →     903 XP
    ///   Level  5 →   1 703 XP
    ///   Level 10 →  11 106 XP
    ///   Level 20 →  60 440 XP
    ///   Level 50 → 482 533 XP
    /// </summary>
    public class LevelCalculator
    {
        private readonly double _base;
        private readonly double _exponent;

        public LevelCalculator(double levelBase = 100, double levelExponent = 1.5)
        {
            _base = levelBase;
            _exponent = levelExponent;
        }

        /// <summary>XP required to advance from level <paramref name="level"/> to level+1.</summary>
        public long XpRequiredForLevel(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), "Level must be >= 1");
            return (long)Math.Round(_base * Math.Pow(level, _exponent));
        }

        /// <summary>Total cumulative XP needed to enter <paramref name="level"/> (level 1 = 0).</summary>
        public long TotalXpForLevel(int level)
        {
            if (level < 1) return 0;
            long total = 0;
            for (int l = 1; l < level; l++)
                total += XpRequiredForLevel(l);
            return total;
        }

        /// <summary>
        /// Decomposes <paramref name="totalXp"/> into the current level, XP already accumulated
        /// into that level, and XP still needed to advance to the next level.
        /// </summary>
        public (int Level, long XpIntoLevel, long XpToNextLevel) GetLevelInfo(long totalXp)
        {
            int level = 1;
            long consumed = 0;

            while (true)
            {
                long needed = XpRequiredForLevel(level);
                if (consumed + needed > totalXp)
                {
                    return (level, totalXp - consumed, needed - (totalXp - consumed));
                }
                consumed += needed;
                level++;
            }
        }

        /// <summary>Returns just the level for a given total XP amount.</summary>
        public int GetLevelFromTotalXp(long totalXp)
            => GetLevelInfo(totalXp).Level;
    }
}
