namespace DailyChallenges.Services;

/// <summary>The "Xp" configuration section: submission and win XP, the streak bonus and the level curve.</summary>
public sealed class XpConfig
{
    public int BaseXpPerSubmission { get; set; } = 50;
    /// <summary>Fraction added per streak day beyond the first (default 1 %).</summary>
    public double StreakBonusPerDay { get; set; } = 0.01;
    /// <summary>Maximum streak bonus fraction (default 30 % = 30-day cap).</summary>
    public double MaxStreakBonus { get; set; } = 0.30;
    public double LevelBase { get; set; } = 100;
    public double LevelExponent { get; set; } = 1.5;
    /// <summary>XP awarded for winning a scoring day (default 100).</summary>
    public int WinXp { get; set; } = 100;
}
