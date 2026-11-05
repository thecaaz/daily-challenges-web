using DailyChallenges.Models;
using DailyChallenges.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.IntegrationTests;

/// <summary>
/// A failed batch insert (e.g. a concurrent unlock of the same achievement) must not leave Added entities
/// behind, or every later SaveChanges in the same scope fails again (B6 "achievement poison").
/// </summary>
public class BatchInsertFailureTests
{
    [Fact]
    public async Task Failed_achievement_batch_does_not_poison_the_context()
    {
        await using var db = new TestDb();
        db.Ctx.Users.Add(new User { Id = 1, Username = "u" });
        db.Ctx.UserAchievements.Add(new UserAchievement { UserId = 1, AchievementId = "win_1", UnlockedAt = DateTime.UtcNow });
        await db.Ctx.SaveChangesAsync();
        var repo = new EfUserAchievementRepository(db.Ctx);

        await Assert.ThrowsAsync<DbUpdateException>(() => repo.AddBatchAsync(new List<UserAchievement>
        {
            new() { UserId = 1, AchievementId = "win_1", UnlockedAt = DateTime.UtcNow },
        }));

        Assert.DoesNotContain(db.Ctx.ChangeTracker.Entries(), e => e.State == EntityState.Added);
        db.Ctx.Users.Add(new User { Id = 2, Username = "later" });
        await db.Ctx.SaveChangesAsync(); // would rethrow the unique violation if the failed insert were still pending
        Assert.Equal(2, await db.Ctx.Users.CountAsync());
    }

    [Fact]
    public async Task Failed_notification_batch_does_not_poison_the_context()
    {
        await using var db = new TestDb();
        var repo = new EfNotificationRepository(db.Ctx);

        // UserId 42 does not exist: foreign key violation.
        await Assert.ThrowsAsync<DbUpdateException>(() => repo.CreateBatchAsync(new List<Notification>
        {
            new() { UserId = 42, Message = "m", Type = "achievement", ScoringDay = DateTime.UtcNow.Date, CreatedAt = DateTime.UtcNow },
        }));

        Assert.DoesNotContain(db.Ctx.ChangeTracker.Entries(), e => e.State == EntityState.Added);
        db.Ctx.Users.Add(new User { Id = 1, Username = "later" });
        await db.Ctx.SaveChangesAsync();
    }
}
