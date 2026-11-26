using DailyChallenges.Exceptions;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services;
using DailyChallenges.TestUtilities;
using Moq;
using static DailyChallenges.Tests.TestEntities;

namespace DailyChallenges.Tests;

public class LeagueServiceTests
{
    private record Sut(
        LeagueService Service,
        Mock<ILeagueRepository> LeagueRepo,
        Mock<IUserProfileRepository> UserRepo
    );

    private static Sut Build()
    {
        var leagueRepo = new Mock<ILeagueRepository>();
        var userRepo = new Mock<IUserProfileRepository>();
        var svc = new LeagueService(leagueRepo.Object, userRepo.Object, new FixedTimeProvider(DateTime.UtcNow));
        return new Sut(svc, leagueRepo, userRepo);
    }

    [Fact]
    public async Task CreateLeague_ValidName_ReturnsDto()
    {
        var sut = Build();
        var owner = MakeUser(1, "alice");
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(owner);
        League? created = null;
        sut.LeagueRepo.Setup(r => r.CreateAsync(It.IsAny<League>()))
            .ReturnsAsync((League l) => { l.Id = 10; created = l; return l; });

        var result = await sut.Service.CreateLeagueAsync(1, "Weekend Warriors", CancellationToken.None);

        Assert.Equal(10, result.Id);
        Assert.Equal("Weekend Warriors", result.Name);
        Assert.Equal(1, result.OwnerId);
        Assert.Equal(1, result.MemberCount);
        var ownerMembershipInsertedWithLeague = Assert.Single(created!.Members);
        Assert.Equal(1, ownerMembershipInsertedWithLeague.UserId);
        Assert.Equal(LeagueRole.Owner, ownerMembershipInsertedWithLeague.Role);
    }

    [Fact]
    public async Task CreateLeague_EmptyName_Throws()
    {
        var sut = Build();

        await Assert.ThrowsAsync<ValidationException>(() => sut.Service.CreateLeagueAsync(1, "   ", CancellationToken.None));
    }

    [Fact]
    public async Task CreateLeague_NameTooLong_Throws()
    {
        var sut = Build();
        var longName = new string('x', 101);

        await Assert.ThrowsAsync<ValidationException>(() => sut.Service.CreateLeagueAsync(1, longName, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteLeague_ByOwner_Succeeds()
    {
        var sut = Build();
        sut.LeagueRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeLeague(1, ownerId: 42));
        sut.LeagueRepo.Setup(r => r.DeleteAsync(1)).ReturnsAsync(true);

        await sut.Service.DeleteLeagueAsync(1, 42, CancellationToken.None);

        sut.LeagueRepo.Verify(r => r.DeleteAsync(1), Times.Once);
    }

    [Fact]
    public async Task DeleteLeague_ByNonOwner_Throws()
    {
        var sut = Build();
        sut.LeagueRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeLeague(1, ownerId: 42));

        await Assert.ThrowsAsync<ForbiddenException>(
            () => sut.Service.DeleteLeagueAsync(1, requestingUserId: 99, CancellationToken.None));

        sut.LeagueRepo.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task LeaveLeague_NonOwnerMember_Succeeds()
    {
        var sut = Build();
        sut.LeagueRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeLeague(1, ownerId: 10));
        var member = new LeagueMember { LeagueId = 1, UserId = 20 };
        sut.LeagueRepo.Setup(r => r.GetMemberAsync(1, 20)).ReturnsAsync(member);
        sut.LeagueRepo.Setup(r => r.RemoveMemberAsync(member)).Returns(Task.CompletedTask);

        await sut.Service.LeaveLeagueAsync(1, userId: 20, CancellationToken.None);

        sut.LeagueRepo.Verify(r => r.RemoveMemberAsync(member), Times.Once);
    }

    [Fact]
    public async Task LeaveLeague_OwnerAttempt_Throws()
    {
        var sut = Build();
        sut.LeagueRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeLeague(1, ownerId: 10));

        await Assert.ThrowsAsync<ValidationException>(
            () => sut.Service.LeaveLeagueAsync(1, userId: 10, CancellationToken.None));
    }

    [Fact]
    public async Task KickMember_ByOwner_RemovesMember()
    {
        var sut = Build();
        sut.LeagueRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeLeague(1, ownerId: 10));
        var member = new LeagueMember { LeagueId = 1, UserId = 20 };
        sut.LeagueRepo.Setup(r => r.GetMemberAsync(1, 20)).ReturnsAsync(member);
        sut.LeagueRepo.Setup(r => r.RemoveMemberAsync(member)).Returns(Task.CompletedTask);

        await sut.Service.KickMemberAsync(1, requestingUserId: 10, targetUserId: 20, CancellationToken.None);

        sut.LeagueRepo.Verify(r => r.RemoveMemberAsync(member), Times.Once);
    }

    [Fact]
    public async Task KickMember_ByNonOwner_Throws()
    {
        var sut = Build();
        sut.LeagueRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeLeague(1, ownerId: 10));

        await Assert.ThrowsAsync<ForbiddenException>(
            () => sut.Service.KickMemberAsync(1, requestingUserId: 99, targetUserId: 20, CancellationToken.None));
    }

    [Fact]
    public async Task RenameLeague_ByOwner_UpdatesName()
    {
        var sut = Build();
        var league = MakeLeague(1, ownerId: 10);
        sut.LeagueRepo.Setup(r => r.GetByIdWithMembersAsync(1)).ReturnsAsync(league);
        sut.LeagueRepo.Setup(r => r.RenameAsync(1, "New Name")).ReturnsAsync(true);

        var result = await sut.Service.RenameLeagueAsync(1, requestingUserId: 10, "  New Name ", CancellationToken.None);

        Assert.Equal("New Name", result.Name);
        sut.LeagueRepo.Verify(r => r.RenameAsync(1, "New Name"), Times.Once);
    }

    [Fact]
    public async Task RenameLeague_ByNonOwner_Throws()
    {
        var sut = Build();
        sut.LeagueRepo.Setup(r => r.GetByIdWithMembersAsync(1)).ReturnsAsync(MakeLeague(1, ownerId: 10));

        await Assert.ThrowsAsync<ForbiddenException>(
            () => sut.Service.RenameLeagueAsync(1, requestingUserId: 99, "New Name", CancellationToken.None));
    }
}
