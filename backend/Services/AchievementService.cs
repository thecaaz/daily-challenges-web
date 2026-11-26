using DailyChallenges.Achievements;
using DailyChallenges.DTOs;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Services;

public sealed class AchievementService(
    IUserAchievementRepository userAchievements,
    IUserRepository users,
    AchievementProgressReader progress,
    INotificationRepository notifications,
    TimeProvider time,
    ILogger<AchievementService> logger) : IAchievementService
{
    /// <summary>
    /// A boundary: callers run this after committing their own work (submission, day win, friendship), and a
    /// failed achievement check must never break or undo that, so every failure is logged and swallowed here.
    /// </summary>
    public async Task CheckAndAwardAsync(int userId, AchievementTrigger trigger)
    {
        try
        {
            await AwardNewlyEarnedAsync(userId, trigger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Achievement check failed for user {UserId} on trigger {Trigger}", userId, trigger);
        }
    }

    public async Task<List<AchievementDto>> GetForUserAsync(int userId, CancellationToken ct)
    {
        var unlocked = await userAchievements.GetUnlockedWithTimestampsAsync(userId);

        return AchievementCatalog.All.Select(def => new AchievementDto
        {
            AchievementId = def.Id,
            Name = def.Name,
            Description = def.Description,
            IconKey = def.IconKey,
            UnlockedAt = unlocked.TryGetValue(def.Id, out var unlockedAt) ? unlockedAt : null
        }).ToList();
    }

    public IReadOnlyList<AchievementCatalogEntryDto> GetCatalog() =>
        AchievementCatalog.All
            .Select(def => new AchievementCatalogEntryDto(def.Id, def.Name, def.Description, def.IconKey))
            .ToList();

    private async Task AwardNewlyEarnedAsync(int userId, AchievementTrigger trigger)
    {
        var candidates = await GetLockedCandidatesAsync(userId, trigger);
        if (candidates.Count == 0) return;

        var user = await users.GetByIdAsync(userId);
        if (user == null) return;

        var values = await progress.GetAsync(user, candidates.Select(def => def.Metric));
        var newlyUnlocked = candidates.Where(def => values[def.Metric] >= def.Threshold).ToList();
        if (newlyUnlocked.Count == 0) return;

        await SaveUnlocksAsync(userId, newlyUnlocked);
    }

    private async Task<List<AchievementDefinition>> GetLockedCandidatesAsync(int userId, AchievementTrigger trigger)
    {
        var alreadyUnlockedIds = await userAchievements.GetUnlockedIdsAsync(userId);
        return AchievementCatalog.All
            .Where(def => def.Trigger == trigger && !alreadyUnlockedIds.Contains(def.Id))
            .ToList();
    }

    private async Task SaveUnlocksAsync(int userId, List<AchievementDefinition> newlyUnlocked)
    {
        var now = time.GetUtcNow().UtcDateTime;

        foreach (var def in newlyUnlocked)
            logger.LogInformation("User {UserId} unlocked achievement {AchievementId}", userId, def.Id);

        await userAchievements.AddBatchAsync(newlyUnlocked.Select(def => new UserAchievement
        {
            UserId = userId,
            AchievementId = def.Id,
            UnlockedAt = now
        }).ToList());
        await notifications.CreateBatchAsync(newlyUnlocked.Select(def => new Notification
        {
            UserId = userId,
            GameId = null,
            ScoringDay = now.Date,
            Message = $"Achievement unlocked: {def.Name}",
            Type = NotificationTypes.Achievement,
            IsRead = false,
            CreatedAt = now
        }).ToList());
    }
}
