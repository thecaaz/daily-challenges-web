using DailyChallenges.Exceptions;
using DailyChallenges.DTOs;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services;
using DailyChallenges.Services.Contracts;
using DailyChallenges.TestUtilities;
using Moq;

namespace DailyChallenges.Tests;

public class LeagueServiceTests
{
    // ── Builder ───────────────────────────────────────────────────────────────

    private record Sut(
        LeagueService Service,
        Mock<ILeagueRepository> LeagueRepo,
        Mock<IUserProfileRepository> UserRepo,
        Mock<INotificationRepository> NotifRepo,
        Mock<IGameRepository> GameRepo
    );

    private static Sut Build()
    {
        var leagueRepo = new Mock<ILeagueRepository>();
        var userRepo = new Mock<IUserProfileRepository>();
        var notifRepo = new Mock<INotificationRepository>();
        var gameRepo = new Mock<IGameRepository>();

        notifRepo.Setup(r => r.CreateBatchAsync(It.IsAny<List<Notification>>())).Returns(Task.CompletedTask);

        var svc = new LeagueService(leagueRepo.Object, userRepo.Object, notifRepo.Object, gameRepo.Object, new FixedTimeProvider(DateTime.UtcNow),
            new Mock<IScoreVisibilityService>().Object, new Mock<IScreenshotUrlSigner>().Object);
        return new Sut(svc, leagueRepo, userRepo, notifRepo, gameRepo);
    }

    private static User MakeUser(int id, string username = "user") =>
        new() { Id = id, Username = username, Level = 1, Streak = 0, TotalXp = 0 };

    private static League MakeLeague(int id, int ownerId, string name = "My League") =>
        new()
        {
            Id = id,
            Name = name,
            OwnerId = ownerId,
            Owner = MakeUser(ownerId, "owner"),
            CreatedAt = DateTime.UtcNow,
            Members = new List<LeagueMember>
            {
                new() { Id = 1, LeagueId = id, UserId = ownerId, Role = LeagueRole.Owner, User = MakeUser(ownerId, "owner") }
            },
            Invitations = new List<LeagueInvitation>()
        };

    // ── CreateLeagueAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateLeague_ValidName_ReturnsDto()
    {
        var sut = Build();
        var owner = MakeUser(1, "alice");

        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(owner);
        League? created = null;
        sut.LeagueRepo.Setup(r => r.CreateAsync(It.IsAny<League>()))
            .ReturnsAsync((League l) => { l.Id = 10; created = l; return l; });

        var result = await sut.Service.CreateLeagueAsync(1, "Weekend Warriors");

        Assert.Equal(10, result.Id);
        Assert.Equal("Weekend Warriors", result.Name);
        Assert.Equal(1, result.OwnerId);
        Assert.Equal(1, result.MemberCount);
        // The owner's membership is inserted together with the league (one SaveChanges).
        var membership = Assert.Single(created!.Members);
        Assert.Equal(1, membership.UserId);
        Assert.Equal(LeagueRole.Owner, membership.Role);
    }

    [Fact]
    public async Task CreateLeague_EmptyName_Throws()
    {
        var sut = Build();
        await Assert.ThrowsAsync<ArgumentException>(() => sut.Service.CreateLeagueAsync(1, "   "));
    }

    [Fact]
    public async Task CreateLeague_NameTooLong_Throws()
    {
        var sut = Build();
        var longName = new string('x', 101);
        await Assert.ThrowsAsync<ArgumentException>(() => sut.Service.CreateLeagueAsync(1, longName));
    }

    // ── DeleteLeagueAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteLeague_ByOwner_Succeeds()
    {
        var sut = Build();
        sut.LeagueRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeLeague(1, ownerId: 42));
        sut.LeagueRepo.Setup(r => r.DeleteAsync(1)).ReturnsAsync(true);

        await sut.Service.DeleteLeagueAsync(1, 42);

        sut.LeagueRepo.Verify(r => r.DeleteAsync(1), Times.Once);
    }

    [Fact]
    public async Task DeleteLeague_ByNonOwner_Throws()
    {
        var sut = Build();
        sut.LeagueRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeLeague(1, ownerId: 42));

        await Assert.ThrowsAsync<ForbiddenException>(
            () => sut.Service.DeleteLeagueAsync(1, requestingUserId: 99));
        sut.LeagueRepo.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    // ── InviteByUsernameAsync ─────────────────────────────────────────────────

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
        sut.LeagueRepo.Setup(r => r.GetPendingInvitationAsync(1, 20)).ReturnsAsync((LeagueInvitation?)null);
        sut.LeagueRepo.Setup(r => r.CreateInvitationAsync(It.IsAny<LeagueInvitation>()))
            .ReturnsAsync((LeagueInvitation i) => { i.Id = 5; return i; });

        var result = await sut.Service.InviteByUsernameAsync(1, inviterId: 10, username: "bob");

        Assert.Equal(5, result.Id);
        Assert.Equal(1, result.LeagueId);
        Assert.Equal(10, result.InviterId);
        Assert.Equal(20, result.InviteeId);
        // Notification sent to invitee
        sut.NotifRepo.Verify(r => r.CreateBatchAsync(It.IsAny<List<Notification>>()), Times.Once);
    }

    [Fact]
    public async Task InviteByUsername_AlreadyMember_Throws()
    {
        var sut = Build();
        var league = MakeLeague(1, ownerId: 10);
        // Add invitee as existing member
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
        sut.LeagueRepo.Setup(r => r.GetPendingInvitationAsync(1, 20)).ReturnsAsync(existing);

        await Assert.ThrowsAsync<ConflictException>(
            () => sut.Service.InviteByUsernameAsync(1, inviterId: 10, username: "bob"));
    }

    [Fact]
    public async Task InviteByUsername_DeclinedWithin24h_Throws()
    {
        var sut = Build();
        sut.LeagueRepo.Setup(r => r.GetByIdWithMembersAsync(1)).ReturnsAsync(MakeLeague(1, ownerId: 10));
        sut.UserRepo.Setup(r => r.GetByUsernameAsync("bob")).ReturnsAsync(MakeUser(20, "bob"));
        sut.LeagueRepo.Setup(r => r.GetLastDeclinedAtAsync(1, 20)).ReturnsAsync(DateTime.UtcNow.AddHours(-23));

        await Assert.ThrowsAsync<ConflictException>(
            () => sut.Service.InviteByUsernameAsync(1, inviterId: 10, username: "bob"));
        sut.LeagueRepo.Verify(r => r.CreateInvitationAsync(It.IsAny<LeagueInvitation>()), Times.Never);
    }

    [Fact]
    public async Task AcceptInvitation_ClaimLost_ThrowsConflict()
    {
        var sut = Build();
        sut.LeagueRepo.Setup(r => r.GetInvitationByIdAsync(5)).ReturnsAsync(new LeagueInvitation
            { Id = 5, LeagueId = 1, InviterId = 10, InviteeId = 20, Status = LeagueInvitationStatus.Pending });
        sut.LeagueRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeLeague(1, ownerId: 10));
        sut.LeagueRepo.Setup(r => r.AcceptInvitationAsync(5, 20, It.IsAny<DateTime>()))
            .ReturnsAsync(AcceptInvitationResult.NotPending);

        await Assert.ThrowsAsync<ConflictException>(() => sut.Service.AcceptInvitationAsync(5, 20));
        sut.NotifRepo.Verify(r => r.CreateBatchAsync(It.IsAny<List<Notification>>()), Times.Never);
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

    // ── LeaveLeagueAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task LeaveLeague_NonOwnerMember_Succeeds()
    {
        var sut = Build();
        sut.LeagueRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeLeague(1, ownerId: 10));
        var member = new LeagueMember { LeagueId = 1, UserId = 20 };
        sut.LeagueRepo.Setup(r => r.GetMemberAsync(1, 20)).ReturnsAsync(member);
        sut.LeagueRepo.Setup(r => r.RemoveMemberAsync(member)).Returns(Task.CompletedTask);

        await sut.Service.LeaveLeagueAsync(1, userId: 20);

        sut.LeagueRepo.Verify(r => r.RemoveMemberAsync(member), Times.Once);
    }

    [Fact]
    public async Task LeaveLeague_OwnerAttempt_Throws()
    {
        var sut = Build();
        sut.LeagueRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeLeague(1, ownerId: 10));

        await Assert.ThrowsAsync<ValidationException>(
            () => sut.Service.LeaveLeagueAsync(1, userId: 10));
    }

    // ── KickMemberAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task KickMember_ByOwner_RemovesMember()
    {
        var sut = Build();
        sut.LeagueRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeLeague(1, ownerId: 10));
        var member = new LeagueMember { LeagueId = 1, UserId = 20 };
        sut.LeagueRepo.Setup(r => r.GetMemberAsync(1, 20)).ReturnsAsync(member);
        sut.LeagueRepo.Setup(r => r.RemoveMemberAsync(member)).Returns(Task.CompletedTask);

        await sut.Service.KickMemberAsync(1, requestingUserId: 10, targetUserId: 20);

        sut.LeagueRepo.Verify(r => r.RemoveMemberAsync(member), Times.Once);
    }

    [Fact]
    public async Task KickMember_ByNonOwner_Throws()
    {
        var sut = Build();
        sut.LeagueRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeLeague(1, ownerId: 10));

        await Assert.ThrowsAsync<ForbiddenException>(
            () => sut.Service.KickMemberAsync(1, requestingUserId: 99, targetUserId: 20));
    }

    // ── RenameLeagueAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task RenameLeague_ByOwner_UpdatesName()
    {
        var sut = Build();
        var league = MakeLeague(1, ownerId: 10);
        sut.LeagueRepo.Setup(r => r.GetByIdWithMembersAsync(1)).ReturnsAsync(league);
        sut.LeagueRepo.Setup(r => r.RenameAsync(1, "New Name")).ReturnsAsync(true);

        var result = await sut.Service.RenameLeagueAsync(1, requestingUserId: 10, "  New Name ");

        Assert.Equal("New Name", result.Name);
        sut.LeagueRepo.Verify(r => r.RenameAsync(1, "New Name"), Times.Once);
    }

    [Fact]
    public async Task RenameLeague_ByNonOwner_Throws()
    {
        var sut = Build();
        sut.LeagueRepo.Setup(r => r.GetByIdWithMembersAsync(1)).ReturnsAsync(MakeLeague(1, ownerId: 10));

        await Assert.ThrowsAsync<ForbiddenException>(
            () => sut.Service.RenameLeagueAsync(1, requestingUserId: 99, "New Name"));
    }

    [Fact]
    public async Task GetLeagueGameSummaries_NonMember_Throws()
    {
        var sut = Build();
        sut.LeagueRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeLeague(1, ownerId: 10));
        sut.LeagueRepo.Setup(r => r.GetMemberAsync(1, 99)).ReturnsAsync((LeagueMember?)null);
        await Assert.ThrowsAsync<ForbiddenException>(
            () => sut.Service.GetLeagueGameSummariesAsync(1, requestingUserId: 99, days: 7, page: 1, pageSize: 20));
    }

    [Fact]
    public async Task GetLeagueGameSummaries_AsMember_ReturnsList()
    {
        var sut = Build();
        var league = MakeLeague(1, ownerId: 10);
        sut.LeagueRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(league);
        sut.LeagueRepo.Setup(r => r.GetMemberAsync(1, 1)).ReturnsAsync(new LeagueMember { LeagueId = 1, UserId = 1 });
        var expected = (new List<LeagueGameSummaryDto>
        {
            new LeagueGameSummaryDto { GameId = 5, GameName = "G" }
        }, 1);

        sut.LeagueRepo.Setup(r => r.GetLeagueGameSummariesAsync(1, 1, 7, 1, 20)).ReturnsAsync(expected);

        var result = await sut.Service.GetLeagueGameSummariesAsync(1, 1, 7, 1, 20);
        Assert.Equal(expected, result);
    }
}
