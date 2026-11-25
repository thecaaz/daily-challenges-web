using DailyChallenges.Exceptions;
using DailyChallenges.Achievements;
using DailyChallenges.DTOs;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services;
using DailyChallenges.Services.Contracts;
using DailyChallenges.TestUtilities;
using Microsoft.Extensions.Options;
using Moq;

namespace DailyChallenges.Tests;

public class FriendServiceTests
{
    // ── Builder ───────────────────────────────────────────────────────────────

    private record Sut(
        FriendRequestService Requests,
        FriendshipService Friendships,
        Mock<IFriendRepository> FriendRepo,
        Mock<IUserProfileRepository> UserRepo,
        Mock<INotificationRepository> NotifRepo,
        Mock<IAchievementService> Achievements
    );

    private static readonly DateTime Now = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

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

        var clock = new FixedTimeProvider(Now);
        var requests = new FriendRequestService(friends.Object, users.Object, notifs.Object, ach.Object, clock);
        var friendships = new FriendshipService(friends.Object, levelCalc, clock);
        return new Sut(requests, friendships, friends, users, notifs, ach);
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
            sut.Requests.SendRequestAsync(1, 1));
    }

    [Fact]
    public async Task SendRequest_TargetNotFound_ThrowsKeyNotFoundException()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync((User?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.Requests.SendRequestAsync(1, 2));
    }

    [Fact]
    public async Task SendRequest_AlreadyFriends_ThrowsConflictException()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeUser(2));
        sut.FriendRepo.Setup(r => r.GetExistingAsync(1, 2)).ReturnsAsync(new FriendRequest
            { Id = 1, SenderId = 1, ReceiverId = 2, Status = FriendRequestStatus.Accepted });

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            sut.Requests.SendRequestAsync(1, 2));
        Assert.Contains("already friends", ex.Message);
    }

    [Fact]
    public async Task SendRequest_PendingRequestExists_ThrowsConflictException()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeUser(2));
        sut.FriendRepo.Setup(r => r.GetExistingAsync(1, 2))
            .ReturnsAsync(PendingRequest(1, 1, 2));

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            sut.Requests.SendRequestAsync(1, 2));
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

        var dto = await sut.Requests.SendRequestAsync(1, 2);

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

        await sut.Requests.SendRequestAsync(1, 2);

        sut.NotifRepo.Verify(r => r.CreateBatchAsync(It.Is<List<Notification>>(
            n => n.Any(x => x.UserId == 2 && x.Type == "friend_request"))), Times.Once);
    }

    private static FriendRequest DeclinedRequest(int id, int senderId, int receiverId, DateTime declinedAt) =>
        new() { Id = id, SenderId = senderId, ReceiverId = receiverId, Status = FriendRequestStatus.Declined, CreatedAt = declinedAt.AddDays(-1), UpdatedAt = declinedAt };

    [Fact]
    public async Task SendRequest_DeclinedWithin24h_ThrowsConflictAndDoesNotReopen()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeUser(2, "bob"));
        sut.FriendRepo.Setup(r => r.GetExistingAsync(1, 2)).ReturnsAsync(DeclinedRequest(7, 1, 2, Now.AddHours(-23)));

        await Assert.ThrowsAsync<ConflictException>(() => sut.Requests.SendRequestAsync(1, 2));
        sut.FriendRepo.Verify(r => r.ReopenDeclinedAsync(It.IsAny<int>(), It.IsAny<FriendRequestDirection>(), It.IsAny<DateTime>()), Times.Never);
        sut.FriendRepo.Verify(r => r.CreateAsync(It.IsAny<FriendRequest>()), Times.Never);
    }

    [Fact]
    public async Task SendRequest_DeclinedOver24hAgo_ReopensTheSameRow()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser(1, "alice"));
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeUser(2, "bob"));
        sut.FriendRepo.Setup(r => r.GetExistingAsync(1, 2)).ReturnsAsync(DeclinedRequest(7, 1, 2, Now.AddHours(-25)));
        sut.FriendRepo.Setup(r => r.ReopenDeclinedAsync(7, new FriendRequestDirection(1, 2), Now)).ReturnsAsync(true);
        sut.FriendRepo.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(PendingRequest(7, 1, 2));

        var dto = await sut.Requests.SendRequestAsync(1, 2);

        Assert.Equal(7, dto.Id);
        sut.FriendRepo.Verify(r => r.CreateAsync(It.IsAny<FriendRequest>()), Times.Never);
    }

    [Fact]
    public async Task SendRequest_TheUserWhoDeclined_CanAskRightAway()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser(1, "alice"));
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeUser(2, "bob"));
        // 1 -> 2 was declined by 2 an hour ago; now 2 asks 1.
        sut.FriendRepo.Setup(r => r.GetExistingAsync(2, 1)).ReturnsAsync(DeclinedRequest(7, 1, 2, Now.AddHours(-1)));
        sut.FriendRepo.Setup(r => r.ReopenDeclinedAsync(7, new FriendRequestDirection(2, 1), Now)).ReturnsAsync(true);
        sut.FriendRepo.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(PendingRequest(7, 2, 1));

        var dto = await sut.Requests.SendRequestAsync(2, 1);

        Assert.Equal(7, dto.Id);
        sut.FriendRepo.Verify(r => r.ReopenDeclinedAsync(7, new FriendRequestDirection(2, 1), Now), Times.Once);
    }

    [Fact]
    public async Task SendRequest_RecentUnreadNotificationFromSameSender_SkipsNotification()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser(1, "alice"));
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeUser(2, "bob"));
        sut.FriendRepo.Setup(r => r.CreateAsync(It.IsAny<FriendRequest>())).ReturnsAsync(PendingRequest(1, 1, 2));
        sut.NotifRepo.Setup(r => r.HasRecentUnreadFromActorAsync(new RecentNotificationQuery(2, "friend_request", 1, Now.AddMinutes(-15)))).ReturnsAsync(true);

        await sut.Requests.SendRequestAsync(1, 2);

        sut.NotifRepo.Verify(r => r.CreateBatchAsync(It.IsAny<List<Notification>>()), Times.Never);
    }

    [Fact]
    public async Task SendRequest_Notification_CarriesActor()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser(1, "alice"));
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeUser(2, "bob"));
        sut.FriendRepo.Setup(r => r.CreateAsync(It.IsAny<FriendRequest>())).ReturnsAsync(PendingRequest(1, 1, 2));

        await sut.Requests.SendRequestAsync(1, 2);

        sut.NotifRepo.Verify(r => r.CreateBatchAsync(It.Is<List<Notification>>(
            n => n.Single().ActorUserId == 1 && n.Single().UserId == 2)), Times.Once);
    }

    [Fact]
    public async Task GetRelationshipStatus_Declined_ReadsAsNone()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetRelationshipAsync(1, 2)).ReturnsAsync(DeclinedRequest(7, 1, 2, Now.AddHours(-1)));

        Assert.Equal("none", await sut.Friendships.GetRelationshipStatusAsync(1, 2));
    }

    [Fact]
    public async Task GetRelationshipStatus_NoRow_ReadsAsNone()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetRelationshipAsync(1, 2)).ReturnsAsync((FriendRequest?)null);

        Assert.Equal("none", await sut.Friendships.GetRelationshipStatusAsync(1, 2));
    }

    [Fact]
    public async Task GetRelationshipStatus_Accepted_ReadsAsFriends()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetRelationshipAsync(1, 2)).ReturnsAsync(new FriendRequest
            { Id = 1, SenderId = 2, ReceiverId = 1, Status = FriendRequestStatus.Accepted });

        Assert.Equal("friends", await sut.Friendships.GetRelationshipStatusAsync(1, 2));
    }

    [Fact]
    public async Task GetRelationshipStatus_Pending_ReadsAsDirection()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetRelationshipAsync(1, 2)).ReturnsAsync(PendingRequest(1, 1, 2));
        sut.FriendRepo.Setup(r => r.GetRelationshipAsync(2, 1)).ReturnsAsync(PendingRequest(1, 1, 2));

        Assert.Equal("pending_sent", await sut.Friendships.GetRelationshipStatusAsync(1, 2));
        Assert.Equal("pending_received", await sut.Friendships.GetRelationshipStatusAsync(2, 1));
    }

    [Fact]
    public async Task SendRequestByUsername_TrimsTheUsername()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByUsernameAsync("bob")).ReturnsAsync(MakeUser(2, "bob"));
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeUser(2, "bob"));
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser(1, "alice"));
        sut.FriendRepo.Setup(r => r.GetExistingAsync(1, 2)).ReturnsAsync((FriendRequest?)null);
        sut.FriendRepo.Setup(r => r.CreateAsync(It.IsAny<FriendRequest>())).ReturnsAsync(PendingRequest(5, 1, 2));

        var dto = await sut.Requests.SendRequestByUsernameAsync(1, "  bob ");

        Assert.Equal(5, dto.Id);
        sut.FriendRepo.Verify(r => r.CreateAsync(It.Is<FriendRequest>(fr => fr.SenderId == 1 && fr.ReceiverId == 2 && fr.CreatedAt == Now)), Times.Once);
    }

    [Fact]
    public async Task SendRequestByUsername_UnknownUser_ThrowsWithTheGivenName()
    {
        var sut = Build();

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.Requests.SendRequestByUsernameAsync(1, " ghost"));

        Assert.Equal("No user found with username ' ghost'.", ex.Message);
    }

    [Fact]
    public async Task SendRequest_DeclinedWithin24h_MessageNamesTheTarget()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeUser(2, "bob"));
        sut.FriendRepo.Setup(r => r.GetExistingAsync(1, 2)).ReturnsAsync(DeclinedRequest(7, 1, 2, Now.AddHours(-1)));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => sut.Requests.SendRequestAsync(1, 2));

        Assert.Equal("bob declined your friend request recently. Try again after 24 hours.", ex.Message);
    }

    [Fact]
    public async Task SendRequest_SenderMissing_ThrowsKeyNotFound()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeUser(2, "bob"));
        sut.FriendRepo.Setup(r => r.GetExistingAsync(1, 2)).ReturnsAsync((FriendRequest?)null);
        sut.FriendRepo.Setup(r => r.CreateAsync(It.IsAny<FriendRequest>())).ReturnsAsync(PendingRequest(5, 1, 2));

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.Requests.SendRequestAsync(1, 2));

        Assert.Equal("Sender not found.", ex.Message);
    }

    [Fact]
    public async Task SendRequest_Notification_HasTheDocumentedContent()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser(1, "alice"));
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeUser(2, "bob"));
        sut.FriendRepo.Setup(r => r.GetExistingAsync(1, 2)).ReturnsAsync((FriendRequest?)null);
        sut.FriendRepo.Setup(r => r.CreateAsync(It.IsAny<FriendRequest>())).ReturnsAsync(PendingRequest(5, 1, 2));

        await sut.Requests.SendRequestAsync(1, 2);

        sut.NotifRepo.Verify(r => r.HasRecentUnreadFromActorAsync(new RecentNotificationQuery(2, "friend_request", 1, Now.AddMinutes(-15))), Times.Once);
        sut.NotifRepo.Verify(r => r.CreateBatchAsync(It.Is<List<Notification>>(n =>
            n.Single().Message == "alice sent you a friend request."
            && n.Single().Type == "friend_request"
            && n.Single().GameId == null
            && n.Single().ScoringDay == Now.Date
            && n.Single().CreatedAt == Now)), Times.Once);
    }

    [Fact]
    public async Task AnswerRequest_Accept_UnknownReceiverNameReadsAsSomeone()
    {
        var sut = Build();
        var fr = PendingRequest(1, 1, 2);
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(fr);
        sut.FriendRepo.Setup(r => r.UpdateAsync(It.IsAny<FriendRequest>())).ReturnsAsync(fr);
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync((User?)null);

        await sut.Requests.AcceptRequestAsync(1, 2);

        Assert.Equal(Now, fr.UpdatedAt);
        sut.NotifRepo.Verify(r => r.CreateBatchAsync(It.Is<List<Notification>>(n =>
            n.Single().Message == "Someone accepted your friend request."
            && n.Single().ActorUserId == 2
            && n.Single().ScoringDay == Now.Date)), Times.Once);
    }

    [Fact]
    public async Task GetFriends_MapsUsersWithLevel()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetFriendsAsync(1)).ReturnsAsync(new List<User> { MakeUser(2, "bob") });

        var friends = await sut.Friendships.GetFriendsAsync(1);

        var friend = Assert.Single(friends);
        Assert.Equal(2, friend.UserId);
        Assert.Equal("bob", friend.Username);
        Assert.Equal(1, friend.Level);
    }

    [Fact]
    public async Task GetIncomingAndOutgoing_MapRequests()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetIncomingPendingAsync(2)).ReturnsAsync(new List<FriendRequest> { PendingRequest(1, 1, 2) });
        sut.FriendRepo.Setup(r => r.GetOutgoingPendingAsync(1)).ReturnsAsync(new List<FriendRequest> { PendingRequest(1, 1, 2) });

        Assert.Equal("pending", Assert.Single(await sut.Friendships.GetIncomingRequestsAsync(2)).Status);
        Assert.Equal(1, Assert.Single(await sut.Friendships.GetOutgoingRequestsAsync(1)).Id);
    }

    // ── Accept / DeclineRequestAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task AnswerRequest_RequestNotFound_ThrowsKeyNotFoundException()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((FriendRequest?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.Requests.AcceptRequestAsync(99, 2));
    }

    [Fact]
    public async Task AnswerRequest_WrongReceiver_ThrowsNotFoundException()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(PendingRequest(1, 1, 2));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.Requests.AcceptRequestAsync(1, 99)); // receiverId=99 but request has receiverId=2
    }

    [Fact]
    public async Task AnswerRequest_AlreadyProcessed_ThrowsConflictException()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new FriendRequest
            { Id = 1, SenderId = 1, ReceiverId = 2, Status = FriendRequestStatus.Accepted });
        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.Requests.AcceptRequestAsync(1, 2));
    }

    [Fact]
    public async Task AnswerRequest_Accept_UpdatesStatusToAccepted()
    {
        var sut = Build();
        var fr = PendingRequest(1, 1, 2);
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(fr);
        sut.FriendRepo.Setup(r => r.UpdateAsync(It.IsAny<FriendRequest>())).ReturnsAsync(fr);
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeUser(2, "bob"));

        await sut.Requests.AcceptRequestAsync(1, 2);

        Assert.Equal(FriendRequestStatus.Accepted, fr.Status);
        sut.FriendRepo.Verify(r => r.UpdateAsync(fr), Times.Once);
    }

    [Fact]
    public async Task AnswerRequest_Accept_TriggersFriendAchievementForBothUsers()
    {
        var sut = Build();
        var fr = PendingRequest(1, 1, 2);
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(fr);
        sut.FriendRepo.Setup(r => r.UpdateAsync(It.IsAny<FriendRequest>())).ReturnsAsync(fr);
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeUser(2));

        await sut.Requests.AcceptRequestAsync(1, 2);

        sut.Achievements.Verify(a => a.CheckAndAwardAsync(1, AchievementTrigger.FriendAccepted), Times.Once);
        sut.Achievements.Verify(a => a.CheckAndAwardAsync(2, AchievementTrigger.FriendAccepted), Times.Once);
    }

    [Fact]
    public async Task AnswerRequest_Accept_SendsAcceptedNotificationToSender()
    {
        var sut = Build();
        var fr = PendingRequest(1, 1, 2);
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(fr);
        sut.FriendRepo.Setup(r => r.UpdateAsync(It.IsAny<FriendRequest>())).ReturnsAsync(fr);
        sut.UserRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeUser(2, "bob"));

        await sut.Requests.AcceptRequestAsync(1, 2);

        sut.NotifRepo.Verify(r => r.CreateBatchAsync(It.Is<List<Notification>>(
            n => n.Any(x => x.UserId == 1 && x.Type == "friend_request_accepted"))), Times.Once);
    }

    [Fact]
    public async Task AnswerRequest_Decline_KeepsRequestAsDeclined()
    {
        var sut = Build();
        var fr = PendingRequest(1, 1, 2);
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(fr);
        sut.FriendRepo.Setup(r => r.UpdateAsync(It.IsAny<FriendRequest>())).ReturnsAsync(fr);

        await sut.Requests.DeclineRequestAsync(1, 2);

        Assert.Equal(FriendRequestStatus.Declined, fr.Status);
        Assert.Equal(Now, fr.UpdatedAt);
        sut.FriendRepo.Verify(r => r.UpdateAsync(fr), Times.Once);
        sut.FriendRepo.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task AnswerRequest_Decline_NoAchievementCheck()
    {
        var sut = Build();
        var fr = PendingRequest(1, 1, 2);
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(fr);
        sut.FriendRepo.Setup(r => r.UpdateAsync(It.IsAny<FriendRequest>())).ReturnsAsync(fr);

        await sut.Requests.DeclineRequestAsync(1, 2);

        sut.Achievements.Verify(a => a.CheckAndAwardAsync(It.IsAny<int>(), It.IsAny<AchievementTrigger>()), Times.Never);
    }

    // ── CancelRequestAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task CancelRequest_RequestNotFound_ThrowsKeyNotFoundException()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((FriendRequest?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.Requests.CancelRequestAsync(1, 1));
    }

    [Fact]
    public async Task CancelRequest_NotSender_ThrowsNotFoundException()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(PendingRequest(1, 1, 2));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.Requests.CancelRequestAsync(1, 99)); // senderId=99 but request has senderId=1
    }

    [Fact]
    public async Task CancelRequest_AlreadyAccepted_ThrowsConflictException()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new FriendRequest
            { Id = 1, SenderId = 1, ReceiverId = 2, Status = FriendRequestStatus.Accepted });
        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.Requests.CancelRequestAsync(1, 1));
    }

    [Fact]
    public async Task CancelRequest_PendingBySender_DeletesRequest()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(PendingRequest(1, 1, 2));
        sut.FriendRepo.Setup(r => r.DeleteAsync(1)).Returns(Task.CompletedTask);

        await sut.Requests.CancelRequestAsync(1, 1);

        sut.FriendRepo.Verify(r => r.DeleteAsync(1), Times.Once);
    }

    // ── RemoveFriendAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveFriend_FriendshipNotFound_ThrowsKeyNotFoundException()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetExistingAsync(1, 2)).ReturnsAsync((FriendRequest?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.Friendships.RemoveFriendAsync(1, 2));
    }

    [Fact]
    public async Task RemoveFriend_NotAccepted_ThrowsNotFoundException()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetExistingAsync(1, 2)).ReturnsAsync(PendingRequest(1, 1, 2));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.Friendships.RemoveFriendAsync(1, 2));
    }

    [Fact]
    public async Task RemoveFriend_AcceptedFriendship_Deletes()
    {
        var sut = Build();
        sut.FriendRepo.Setup(r => r.GetExistingAsync(1, 2)).ReturnsAsync(new FriendRequest
            { Id = 5, SenderId = 1, ReceiverId = 2, Status = FriendRequestStatus.Accepted });
        sut.FriendRepo.Setup(r => r.DeleteAsync(5)).Returns(Task.CompletedTask);

        await sut.Friendships.RemoveFriendAsync(1, 2);

        sut.FriendRepo.Verify(r => r.DeleteAsync(5), Times.Once);
    }
}
