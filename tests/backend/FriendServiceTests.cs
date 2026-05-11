using DailyChallenges.Achievements;
using DailyChallenges.DTOs;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services;
using DailyChallenges.Services.Contracts;
using Microsoft.Extensions.Options;
using Moq;

namespace DailyChallenges.Tests;

public class FriendServiceTests
{
    // ── Builder ───────────────────────────────────────────────────────────────

    private record Sut(
        FriendService Service,
        Mock<IFriendRepository> FriendRepo,
        Mock<IUserProfileRepository> UserRepo,
        Mock<INotificationRepository> NotifRepo,
        Mock<IAchievementService> Achievements
    );

    private static Sut Build()
    {
        var friends   = new Mock<IFriendRepository>();
        var users     = new Mock<IUserProfileRepository>();
        var notifs    = new Mock<INotificationRepository>();
        var ach       = new Mock<IAchievementService>();
        var levelCalc = new LevelCalculator();

        notifs.Setup(r => r.CreateBatchAsync(It.IsAny<List<Notification>>())).Returns(Task.CompletedTask);
        notifs.Setup(r => r.GetByUserPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
              .ReturnsAsync((new List<Notification>(), 0, 0));

        var svc = new FriendService(friends.Object, users.Object, notifs.Object, levelCalc, ach.Object);
        return new Sut(svc, friends, users, notifs, ach);
    }

    private static User MakeUser(int id, string username = "user") =>
        new() { Id = id, Username = username, TotalXp = 0, Level = 1, Streak = 0 };

    private static FriendRequest PendingRequest(int id, int senderId, int receiverId) =>
        new() { Id = id, SenderId = senderId, ReceiverId = receiverId, Status = FriendRequestStatus.Pending, CreatedAt = DateTime.UtcNow };

    // ── SendRequestAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SendRequest_ToSelf_ThrowsArgumentException()
    {
        var sut = Build();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.Service.SendRequestAsync(1, 1));
    }

    [Fact]
    public async Task SendRequest_TargetNotFound_ThrowsKeyNotFoundException()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync((User?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.Service.SendRequestAsync(1, 2));
    }

    [Fact]
    public async Task SendRequest_AlreadyFriends_ThrowsInvalidOperationException()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeUser(2));
        sut.FriendRepo.Setup(r => r.GetExistingAsync(1, 2)).ReturnsAsync(new FriendRequest
            { Id = 1, SenderId = 1, ReceiverId = 2, Status = FriendRequestStatus.Accepted });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.Service.SendRequestAsync(1, 2));
        Assert.Contains("already friends", ex.Message);
    }

    [Fact]
    public async Task SendRequest_PendingRequestExists_ThrowsInvalidOperationException()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeUser(2));
        sut.FriendRepo.Setup(r => r.GetExistingAsync(1, 2))
            .ReturnsAsync(PendingRequest(1, 1, 2));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.Service.SendRequestAsync(1, 2));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task SendRequest_ValidNewRequest_CreatesRequestAndReturnsDto()
    {
        var sut = Build();
        var sender = MakeUser(1, "alice");
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeUser(2, "bob"));
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(sender);
        sut.FriendRepo.Setup(r => r.GetExistingAsync(1, 2)).ReturnsAsync((FriendRequest?)null);

        var created = PendingRequest(99, 1, 2);
        sut.FriendRepo.Setup(r => r.CreateAsync(It.IsAny<FriendRequest>())).ReturnsAsync(created);

        var dto = await sut.Service.SendRequestAsync(1, 2);

        Assert.Equal(99, dto.Id);
        sut.FriendRepo.Verify(r => r.CreateAsync(It.IsAny<FriendRequest>()), Times.Once);
    }

    [Fact]
    public async Task SendRequest_ValidRequest_SendsNotificationToReceiver()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeUser(2, "bob"));
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser(1, "alice"));
        sut.FriendRepo.Setup(r => r.GetExistingAsync(1, 2)).ReturnsAsync((FriendRequest?)null);
        sut.FriendRepo.Setup(r => r.CreateAsync(It.IsAny<FriendRequest>()))
            .ReturnsAsync(PendingRequest(1, 1, 2));

        await sut.Service.SendRequestAsync(1, 2);

        sut.NotifRepo.Verify(r => r.CreateBatchAsync(It.Is<List<Notification>>(
            n => n.Any(x => x.UserId == 2 && x.Type == "friend_request"))), Times.Once);
    }

    // ── RespondToRequestAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task RespondToRequest_RequestNotFound_ThrowsKeyNotFoundException()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((FriendRequest?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.Service.RespondToRequestAsync(99, 2, true));
    }

    [Fact]
    public async Task RespondToRequest_WrongReceiver_ThrowsInvalidOperationException()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(PendingRequest(1, 1, 2));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.Service.RespondToRequestAsync(1, 99, true)); // receiverId=99 but request has receiverId=2
    }

    [Fact]
    public async Task RespondToRequest_AlreadyProcessed_ThrowsInvalidOperationException()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new FriendRequest
            { Id = 1, SenderId = 1, ReceiverId = 2, Status = FriendRequestStatus.Accepted });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.Service.RespondToRequestAsync(1, 2, true));
    }

    [Fact]
    public async Task RespondToRequest_Accept_UpdatesStatusToAccepted()
    {
        var sut = Build();
        var fr = PendingRequest(1, 1, 2);
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(fr);
        sut.FriendRepo.Setup(r => r.UpdateAsync(It.IsAny<FriendRequest>())).ReturnsAsync(fr);
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeUser(2, "bob"));

        await sut.Service.RespondToRequestAsync(1, 2, true);

        Assert.Equal(FriendRequestStatus.Accepted, fr.Status);
        sut.FriendRepo.Verify(r => r.UpdateAsync(fr), Times.Once);
    }

    [Fact]
    public async Task RespondToRequest_Accept_TriggersFriendAchievementForBothUsers()
    {
        var sut = Build();
        var fr = PendingRequest(1, 1, 2);
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(fr);
        sut.FriendRepo.Setup(r => r.UpdateAsync(It.IsAny<FriendRequest>())).ReturnsAsync(fr);
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeUser(2));

        await sut.Service.RespondToRequestAsync(1, 2, true);

        sut.Achievements.Verify(a => a.CheckAndAwardAsync(1, AchievementTrigger.FriendAccepted), Times.Once);
        sut.Achievements.Verify(a => a.CheckAndAwardAsync(2, AchievementTrigger.FriendAccepted), Times.Once);
    }

    [Fact]
    public async Task RespondToRequest_Accept_SendsAcceptedNotificationToSender()
    {
        var sut = Build();
        var fr = PendingRequest(1, 1, 2);
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(fr);
        sut.FriendRepo.Setup(r => r.UpdateAsync(It.IsAny<FriendRequest>())).ReturnsAsync(fr);
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeUser(2, "bob"));

        await sut.Service.RespondToRequestAsync(1, 2, true);

        sut.NotifRepo.Verify(r => r.CreateBatchAsync(It.Is<List<Notification>>(
            n => n.Any(x => x.UserId == 1 && x.Type == "friend_request_accepted"))), Times.Once);
    }

    [Fact]
    public async Task RespondToRequest_Decline_DeletesRequest()
    {
        var sut = Build();
        var fr = PendingRequest(1, 1, 2);
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(fr);
        sut.FriendRepo.Setup(r => r.DeleteAsync(1)).Returns(Task.CompletedTask);

        await sut.Service.RespondToRequestAsync(1, 2, false);

        sut.FriendRepo.Verify(r => r.DeleteAsync(1), Times.Once);
        sut.FriendRepo.Verify(r => r.UpdateAsync(It.IsAny<FriendRequest>()), Times.Never);
    }

    [Fact]
    public async Task RespondToRequest_Decline_NoAchievementCheck()
    {
        var sut = Build();
        var fr = PendingRequest(1, 1, 2);
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(fr);
        sut.FriendRepo.Setup(r => r.DeleteAsync(1)).Returns(Task.CompletedTask);

        await sut.Service.RespondToRequestAsync(1, 2, false);

        sut.Achievements.Verify(a => a.CheckAndAwardAsync(It.IsAny<int>(), It.IsAny<AchievementTrigger>()), Times.Never);
    }

    // ── CancelRequestAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task CancelRequest_RequestNotFound_ThrowsKeyNotFoundException()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((FriendRequest?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.Service.CancelRequestAsync(1, 1));
    }

    [Fact]
    public async Task CancelRequest_NotSender_ThrowsInvalidOperationException()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(PendingRequest(1, 1, 2));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.Service.CancelRequestAsync(1, 99)); // senderId=99 but request has senderId=1
    }

    [Fact]
    public async Task CancelRequest_AlreadyAccepted_ThrowsInvalidOperationException()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new FriendRequest
            { Id = 1, SenderId = 1, ReceiverId = 2, Status = FriendRequestStatus.Accepted });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.Service.CancelRequestAsync(1, 1));
    }

    [Fact]
    public async Task CancelRequest_PendingBySender_DeletesRequest()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(PendingRequest(1, 1, 2));
        sut.FriendRepo.Setup(r => r.DeleteAsync(1)).Returns(Task.CompletedTask);

        await sut.Service.CancelRequestAsync(1, 1);

        sut.FriendRepo.Verify(r => r.DeleteAsync(1), Times.Once);
    }

    // ── RemoveFriendAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveFriend_FriendshipNotFound_ThrowsKeyNotFoundException()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetExistingAsync(1, 2)).ReturnsAsync((FriendRequest?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.Service.RemoveFriendAsync(1, 2));
    }

    [Fact]
    public async Task RemoveFriend_NotAccepted_ThrowsInvalidOperationException()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetExistingAsync(1, 2)).ReturnsAsync(PendingRequest(1, 1, 2));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.Service.RemoveFriendAsync(1, 2));
    }

    [Fact]
    public async Task RemoveFriend_AcceptedFriendship_Deletes()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetExistingAsync(1, 2)).ReturnsAsync(new FriendRequest
            { Id = 5, SenderId = 1, ReceiverId = 2, Status = FriendRequestStatus.Accepted });
        sut.FriendRepo.Setup(r => r.DeleteAsync(5)).Returns(Task.CompletedTask);

        await sut.Service.RemoveFriendAsync(1, 2);

        sut.FriendRepo.Verify(r => r.DeleteAsync(5), Times.Once);
    }
}
