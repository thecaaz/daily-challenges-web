using DailyChallenges.DTOs;
using DailyChallenges.Exceptions;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services;
using DailyChallenges.Services.Contracts;
using Moq;
using static DailyChallenges.Tests.LeagueTestData;

namespace DailyChallenges.Tests;

public class LeagueStandingsServiceTests
{
    private record Sut(
        LeagueStandingsService Service,
        Mock<ILeagueRepository> LeagueRepo,
        Mock<ILeagueStandingsRepository> StandingsRepo
    );

    private static Sut Build()
    {
        var leagueRepo = new Mock<ILeagueRepository>();
        var standingsRepo = new Mock<ILeagueStandingsRepository>();
        var svc = new LeagueStandingsService(
            leagueRepo.Object,
            standingsRepo.Object,
            new Mock<IGameRepository>().Object,
            new Mock<IScoreVisibilityService>().Object,
            new Mock<IScreenshotUrlSigner>().Object);
        return new Sut(svc, leagueRepo, standingsRepo);
    }

    [Fact]
    public async Task GetLeagueGameSummaries_NonMember_Throws()
    {
        var sut = Build();
        sut.LeagueRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeLeague(1, ownerId: 10));
        sut.LeagueRepo.Setup(r => r.GetMemberAsync(1, 99)).ReturnsAsync((LeagueMember?)null);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => sut.Service.GetLeagueGameSummariesAsync(new LeagueGameSummaryQuery(1, RequestingUserId: 99, Days: 7, Page: 1, PageSize: 20)));
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
        sut.StandingsRepo.Setup(r => r.GetLeagueGameSummariesAsync(new LeagueGameSummaryQuery(1, 1, 7, 1, 20))).ReturnsAsync(expected);

        var result = await sut.Service.GetLeagueGameSummariesAsync(new LeagueGameSummaryQuery(1, 1, 7, 1, 20));

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task GetLeagueGameSummaries_OutOfRangeWindowAndPaging_AreClamped()
    {
        var sut = Build();
        sut.LeagueRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeLeague(1, ownerId: 10));
        sut.LeagueRepo.Setup(r => r.GetMemberAsync(1, 1)).ReturnsAsync(new LeagueMember { LeagueId = 1, UserId = 1 });
        sut.StandingsRepo.Setup(r => r.GetLeagueGameSummariesAsync(It.IsAny<LeagueGameSummaryQuery>()))
            .ReturnsAsync((new List<LeagueGameSummaryDto>(), 0));

        await sut.Service.GetLeagueGameSummariesAsync(new LeagueGameSummaryQuery(1, 1, Days: 100, Page: 0, PageSize: 1000));

        sut.StandingsRepo.Verify(r => r.GetLeagueGameSummariesAsync(
            new LeagueGameSummaryQuery(1, 1, Days: 31, Page: 1, PageSize: Paging.MaxPageSize)), Times.Once);
    }
}
