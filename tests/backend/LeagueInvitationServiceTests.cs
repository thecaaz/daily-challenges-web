using DailyChallenges.Exceptions;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services;
using DailyChallenges.TestUtilities;
using Moq;
using static DailyChallenges.Tests.TestEntities;

namespace DailyChallenges.Tests;

public class LeagueInvitationServiceTests
{
    private record Sut(
        LeagueInvitationService Service,
        Mock<ILeagueRepository> LeagueRepo,
        Mock<ILeagueInvitationRepository> InvitationRepo,
        Mock<IUserProfileRepository> UserRepo,
        Mock<INotificationRepository> NotifRepo
    );

    private static Sut Build()
    {
        var leagueRepo = new Mock<ILeagueRepository>();
        var invitationRepo = new Mock<ILeagueInvitationRepository>();
        var userRepo = new Mock<IUserProfileRepository>();
        var notifRepo = new Mock<INotificationRepository>();
        notifRepo.Setup(r => r.CreateBatchAsync(It.IsAny<List<Notification>>())).Returns(Task.CompletedTask);
        var time = new FixedTimeProvider(DateTime.UtcNow);

        var leagueService = new LeagueService(leagueRepo.Object, userRepo.Object, time);
        var svc = new LeagueInvitationService(
            leagueRepo.Object, invitationRepo.Object, userRepo.Object, notifRepo.Object, leagueService, time);
        return new Sut(svc, leagueRepo, invitationRepo, userRepo, notifRepo);
    }

    [Fact]
    public async Task InviteByUsername_ValidUser_CreatesInvitation()
    {
        var sut = Build();
        var league = MakeLeague(1, ownerId: 10);
        var invitee = MakeUser(20, "bob");
        var inviter = MakeUser(10, "owner");
        sut.LeagueRepo.Setup(r => r.GetByIdWithMembersAsync(1)).ReturnsAsync(league);
        sut.UserRepo.Setup(r => r.GetByUsernameAsync("bob")).ReturnsAsync(invitee);
        sut.UserRepo.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(inviter);
        sut.InvitationRepo.Setup(r => r.GetPendingInvitationAsync(1, 20)).ReturnsAsync((LeagueInvitation?)null);
        sut.InvitationRepo.Setup(r => r.CreateInvitationAsync(It.IsAny<LeagueInvitation>()))
            .ReturnsAsync((LeagueInvitation i) => { i.Id = 5; return i; });

        var result = await sut.Service.InviteByUsernameAsync(1, inviterId: 10, username: "bob");

        Assert.Equal(5, result.Id);
        Assert.Equal(1, result.LeagueId);
        Assert.Equal(10, result.InviterId);
        Assert.Equal(20, result.InviteeId);
        sut.NotifRepo.Verify(r => r.CreateBatchAsync(It.IsAny<List<Notification>>()), Times.Once);
    }

    [Fact]
    public async Task InviteByUsername_AlreadyMember_Throws()
    {
        var sut = Build();
        var league = MakeLeague(1, ownerId: 10);
        league.Members.Add(new LeagueMember { UserId = 20, User = MakeUser(20, "bob") });
        var invitee = MakeUser(20, "bob");
        sut.LeagueRepo.Setup(r => r.GetByIdWithMembersAsync(1)).ReturnsAsync(league);
        sut.UserRepo.Setup(r => r.GetByUsernameAsync("bob")).ReturnsAsync(invitee);

        await Assert.ThrowsAsync<ConflictException>(
            () => sut.Service.InviteByUsernameAsync(1, inviterId: 10, username: "bob"));
    }

    [Fact]
    public async Task InviteByUsername_PendingAlreadyExists_Throws()
    {
        var sut = Build();
        var league = MakeLeague(1, ownerId: 10);
        var invitee = MakeUser(20, "bob");
        var existing = new LeagueInvitation { Id = 3, LeagueId = 1, InviteeId = 20, Status = LeagueInvitationStatus.Pending };
        sut.LeagueRepo.Setup(r => r.GetByIdWithMembersAsync(1)).ReturnsAsync(league);
        sut.UserRepo.Setup(r => r.GetByUsernameAsync("bob")).ReturnsAsync(invitee);
        sut.InvitationRepo.Setup(r => r.GetPendingInvitationAsync(1, 20)).ReturnsAsync(existing);

        await Assert.ThrowsAsync<ConflictException>(
            () => sut.Service.InviteByUsernameAsync(1, inviterId: 10, username: "bob"));
    }

    [Fact]
    public async Task InviteByUsername_DeclinedWithin24h_Throws()
    {
        var sut = Build();
        sut.LeagueRepo.Setup(r => r.GetByIdWithMembersAsync(1)).ReturnsAsync(MakeLeague(1, ownerId: 10));
        sut.UserRepo.Setup(r => r.GetByUsernameAsync("bob")).ReturnsAsync(MakeUser(20, "bob"));
        sut.InvitationRepo.Setup(r => r.GetLastDeclinedAtAsync(1, 20)).ReturnsAsync(DateTime.UtcNow.AddHours(-23));

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => sut.Service.InviteByUsernameAsync(1, inviterId: 10, username: "bob"));

        Assert.Equal("bob declined an invitation to this league recently. Try again after 24 hours.", ex.Message);
        sut.InvitationRepo.Verify(r => r.CreateInvitationAsync(It.IsAny<LeagueInvitation>()), Times.Never);
    }

    [Fact]
    public async Task InviteByUsername_InviteSelf_Throws()
    {
        var sut = Build();
        var league = MakeLeague(1, ownerId: 10);
        var self = MakeUser(10, "owner");
        sut.LeagueRepo.Setup(r => r.GetByIdWithMembersAsync(1)).ReturnsAsync(league);
        sut.UserRepo.Setup(r => r.GetByUsernameAsync("owner")).ReturnsAsync(self);

        await Assert.ThrowsAsync<ValidationException>(
            () => sut.Service.InviteByUsernameAsync(1, inviterId: 10, username: "owner"));
    }

    [Fact]
    public async Task AcceptInvitation_ClaimLost_ThrowsConflict()
    {
        var sut = Build();
        sut.InvitationRepo.Setup(r => r.GetInvitationByIdAsync(5)).ReturnsAsync(new LeagueInvitation
            { Id = 5, LeagueId = 1, InviterId = 10, InviteeId = 20, Status = LeagueInvitationStatus.Pending });
        sut.LeagueRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeLeague(1, ownerId: 10));
        sut.InvitationRepo.Setup(r => r.AcceptInvitationAsync(5, 20, It.IsAny<DateTime>()))
            .ReturnsAsync(AcceptInvitationResult.NotPending);

        await Assert.ThrowsAsync<ConflictException>(() => sut.Service.AcceptInvitationAsync(5, 20));

        sut.NotifRepo.Verify(r => r.CreateBatchAsync(It.IsAny<List<Notification>>()), Times.Never);
    }
}
