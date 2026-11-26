using DailyChallenges.DTOs;
using DailyChallenges.Exceptions;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services;
using DailyChallenges.Services.Contracts;
using DailyChallenges.TestUtilities;
using Moq;
using static DailyChallenges.Tests.TestEntities;

namespace DailyChallenges.Tests;

public class LeagueStandingsServiceTests
{
    private static readonly DateTime Now = new(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

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
            new Mock<IScreenshotUrlSigner>().Object,
            new FixedTimeProvider(Now));
        return new Sut(svc, leagueRepo, standingsRepo);
    }

    [Fact]
    public async Task GetLeagueGameSummaries_NonMember_Throws()
    {
        var sut = Build();
        sut.LeagueRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeLeague(1, ownerId: 10));
        sut.LeagueRepo.Setup(r => r.GetMemberAsync(1, 99, It.IsAny<CancellationToken>())).ReturnsAsync((LeagueMember?)null);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => sut.Service.GetLeagueGameSummariesAsync(new LeagueGameSummaryQuery(1, RequestingUserId: 99, Days: 7, Page: 1, PageSize: 20), CancellationToken.None));
    }

    private static LeagueSubmissionRow PlayOf(int gameId, int userId = 1) =>
        new(gameId, $"G{gameId}", GameHasIcon: false, RankingMode.Highest, TimeSpan.Zero, userId, "10", 10, Now.Date, Now);

    private static void AsMember(Sut sut)
    {
        sut.LeagueRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeLeague(1, ownerId: 10));
        sut.LeagueRepo.Setup(r => r.GetMemberAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(new LeagueMember { LeagueId = 1, UserId = 1 });
    }

    [Fact]
    public async Task GetLeagueGameSummaries_AsMember_SummarizesEachPlayedGame()
    {
        var sut = Build();
        AsMember(sut);
        sut.StandingsRepo.Setup(r => r.GetMemberSubmissionsAsync(1, Now.Date.AddDays(-7), Now.Date.AddDays(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync([PlayOf(5)]);

        var (items, total) = await sut.Service.GetLeagueGameSummariesAsync(new LeagueGameSummaryQuery(1, 1, 7, 1, 20), CancellationToken.None);

        Assert.Equal(1, total);
        var summary = Assert.Single(items);
        Assert.Equal((5, "G5", 1, 10.0, 1), (summary.GameId, summary.GameName, summary.PlayCount, summary.TopScoreValue, summary.MyRank));
    }

    /// <summary>Mirrors the service's private summary-window limit.</summary>
    private const int MaxSummaryDays = 31;
    private const int OversizedDays = 100;
    private const int ValidDays = 7;
    private const int OversizedPageSize = 1000;
    private const int PageBelowFirst = 0;

    [Fact]
    public async Task GetLeagueGameSummaries_OversizedWindow_IsClampedToMaxSummaryDays()
    {
        var sut = Build();
        AsMember(sut);
        sut.StandingsRepo.Setup(r => r.GetMemberSubmissionsAsync(1, Now.Date.AddDays(-MaxSummaryDays), Now.Date.AddDays(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync([PlayOf(5)]);

        var (items, _) = await sut.Service.GetLeagueGameSummariesAsync(new LeagueGameSummaryQuery(1, 1, Days: OversizedDays, Page: Paging.FirstPage, PageSize: 20), CancellationToken.None);

        var summary = Assert.Single(items);
        Assert.Equal(MaxSummaryDays, summary.RecentPlays.Count);
    }

    [Fact]
    public async Task GetLeagueGameSummaries_OutOfRangePaging_IsClamped()
    {
        var sut = Build();
        AsMember(sut);
        sut.StandingsRepo.Setup(r => r.GetMemberSubmissionsAsync(1, Now.Date.AddDays(-ValidDays), Now.Date.AddDays(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(1, Paging.MaxPageSize + 1).Select(gameId => PlayOf(gameId)).ToList());

        var (items, total) = await sut.Service.GetLeagueGameSummariesAsync(new LeagueGameSummaryQuery(1, 1, Days: ValidDays, Page: PageBelowFirst, PageSize: OversizedPageSize), CancellationToken.None);

        Assert.Equal(Paging.MaxPageSize + 1, total);
        Assert.Equal(Paging.MaxPageSize, items.Count);
    }
}
