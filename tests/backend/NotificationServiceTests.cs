using DailyChallenges.Exceptions;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services;
using Moq;

namespace DailyChallenges.Tests;

public class NotificationServiceTests
{
    private static readonly DateTime Day = new(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

    private static (NotificationService Service, Mock<INotificationRepository> Repo) Build()
    {
        var repo = new Mock<INotificationRepository>();
        return (new NotificationService(repo.Object), repo);
    }

    private static Notification OwnedBy(int userId) =>
        new() { Id = 3, UserId = userId, Message = "m", Type = "day_win", ScoringDay = Day, Rank = 1, IsRead = true, CreatedAt = Day };

    [Fact]
    public async Task GetNotifications_MapsThePage()
    {
        var (service, repo) = Build();
        repo.Setup(r => r.GetByUserPagedAsync(1, 2, 5)).ReturnsAsync((new List<Notification> { OwnedBy(1) }, 7, 4));

        var page = await service.GetNotificationsAsync(1, 2, 5);

        Assert.Equal(7, page.TotalCount);
        Assert.Equal(4, page.UnreadCount);
        var item = Assert.Single(page.Items);
        Assert.Equal("2026-06-15", item.ScoringDay);
        Assert.Equal(1, item.Rank);
        Assert.True(item.IsRead);
    }

    [Fact]
    public async Task Delete_MissingNotification_ThrowsNotFound()
    {
        var (service, repo) = Build();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteOwnAsync(3, 1));

        Assert.Equal("Notification not found", ex.Message);
        repo.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Delete_SomeoneElsesNotification_ThrowsNotFound()
    {
        var (service, repo) = Build();
        repo.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(OwnedBy(2));

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteOwnAsync(3, 1));

        Assert.Equal("Notification not found", ex.Message);
        repo.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Delete_OwnNotification_Deletes()
    {
        var (service, repo) = Build();
        repo.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(OwnedBy(1));

        await service.DeleteOwnAsync(3, 1);

        repo.Verify(r => r.DeleteAsync(3), Times.Once);
    }

    [Fact]
    public async Task Delete_AdminMayDeleteAnyNotification()
    {
        var (service, repo) = Build();
        repo.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(OwnedBy(2));

        await service.DeleteAnyAsync(3);

        repo.Verify(r => r.DeleteAsync(3), Times.Once);
    }

    [Fact]
    public async Task DeleteAny_MissingNotification_ThrowsNotFound()
    {
        var (service, repo) = Build();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAnyAsync(3));

        Assert.Equal("Notification not found", ex.Message);
        repo.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetNotifications_ClampsPaging()
    {
        var (service, repo) = Build();
        repo.Setup(r => r.GetByUserPagedAsync(1, 1, Paging.MaxPageSize)).ReturnsAsync((new List<Notification>(), 0, 0));

        await service.GetNotificationsAsync(1, 0, 5000);

        repo.Verify(r => r.GetByUserPagedAsync(1, 1, Paging.MaxPageSize), Times.Once);
    }
}
