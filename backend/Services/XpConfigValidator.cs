using Microsoft.Extensions.Options;

namespace DailyChallenges.Services
{
    /// <summary>
    /// Rejects an "Xp" configuration the level curve cannot work with, so the app fails at startup
    /// instead of looping in LevelCalculator on the first XP award.
    /// </summary>
    public sealed class XpConfigValidator : IValidateOptions<XpConfig>
    {
        public ValidateOptionsResult Validate(string? name, XpConfig options)
        {
            var errors = new List<string>();
            if (!LevelCalculator.IsValidBase(options.LevelBase))
                errors.Add($"Xp:LevelBase must be a finite number > 0 (got {options.LevelBase}).");
            if (!LevelCalculator.IsValidExponent(options.LevelExponent))
                errors.Add($"Xp:LevelExponent must be a finite number > 0 (got {options.LevelExponent}).");
            if (options.BaseXpPerSubmission < 0)
                errors.Add("Xp:BaseXpPerSubmission must be >= 0.");
            if (options.WinXp < 0)
                errors.Add("Xp:WinXp must be >= 0.");
            if (!double.IsFinite(options.StreakBonusPerDay) || options.StreakBonusPerDay < 0)
                errors.Add("Xp:StreakBonusPerDay must be a finite number >= 0.");
            if (!double.IsFinite(options.MaxStreakBonus) || options.MaxStreakBonus < 0)
                errors.Add("Xp:MaxStreakBonus must be a finite number >= 0.");

            return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
        }
    }
}
