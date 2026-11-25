namespace DailyChallenges.DTOs;

/// <summary>An achievement of the public catalog, without any user's unlock state.</summary>
public sealed record AchievementCatalogEntryDto(string AchievementId, string Name, string Description, string IconKey);
