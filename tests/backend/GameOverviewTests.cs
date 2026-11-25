using System.Security.Claims;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services;
using DailyChallenges.TestUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DailyChallenges.Tests;

public class GameOverviewTests
{
    private const int GameId = 7;

    private readonly Mock<IGameRepository> _games = new();
    private readonly Mock<ISubmissionQueryService> _submissionQueries = new();
    private readonly Mock<IFavoriteRepository> _favorites = new();

    public GameOverviewTests()
    {
        _games.Setup(r => r.GetByIdWithoutBlobAsync(GameId)).ReturnsAsync(new Game { Id = GameId, Name = "overview" });
    }

    private GameQueryService Build() => new(
        _games.Object,
        Mock.Of<ISubmissionRepository>(),
        _submissionQueries.Object,
        _favorites.Object,
        new FixedTimeProvider(new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc)),
        NullLogger<GameQueryService>.Instance);

    [Fact]
    public async Task GetOverviewAsync_PartsFail_ReportsEachFailedPartAndFallsBack()
    {
        _submissionQueries.Setup(q => q.GetAvailableDatesAsync(GameId)).ThrowsAsync(new InvalidOperationException("dates broke"));
        _submissionQueries.Setup(q => q.HasUserSubmittedForLatestAsync(GameId, It.IsAny<ClaimsPrincipal?>())).ThrowsAsync(new TimeoutException("check broke"));

        var overview = await Build().GetOverviewAsync(GameId, null);

        Assert.Empty(overview.AvailableDates!);
        Assert.False(overview.HasSubmittedForLatest);
        Assert.NotNull(overview.Errors);
        Assert.Collection(overview.Errors!,
            e => { Assert.Equal("availableDates", e.Part); Assert.Equal("dates broke", e.Message); },
            e => { Assert.Equal("hasSubmitted", e.Part); Assert.Equal("check broke", e.Message); });
    }

    [Fact]
    public async Task GetOverviewAsync_OnePartFails_KeepsTheOtherPart()
    {
        _submissionQueries.Setup(q => q.GetAvailableDatesAsync(GameId)).ReturnsAsync(new List<string> { "2026-06-15" });
        _submissionQueries.Setup(q => q.HasUserSubmittedForLatestAsync(GameId, It.IsAny<ClaimsPrincipal?>())).ThrowsAsync(new InvalidOperationException("check broke"));

        var overview = await Build().GetOverviewAsync(GameId, null);

        Assert.Equal(new[] { "2026-06-15" }, overview.AvailableDates);
        Assert.Equal("hasSubmitted", Assert.Single(overview.Errors!).Part);
    }

    [Fact]
    public async Task GetOverviewAsync_PartNotIncluded_IsNotQueried()
    {
        var overview = await Build().GetOverviewAsync(GameId, null, "availableDates");

        _submissionQueries.Verify(q => q.HasUserSubmittedForLatestAsync(It.IsAny<int>(), It.IsAny<ClaimsPrincipal?>()), Times.Never);
        Assert.False(overview.HasSubmittedForLatest);
        Assert.Null(overview.Errors);
    }

    [Fact]
    public async Task GetOverviewAsync_MissingGame_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Build().GetOverviewAsync(404, null));
    }
}
