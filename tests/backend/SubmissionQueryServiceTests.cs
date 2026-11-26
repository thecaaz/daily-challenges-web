using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services;
using Moq;

namespace DailyChallenges.Tests;

public class SubmissionQueryServiceTests
{
    private const int GameId = 1;
    private static readonly DateTime CurrentDay = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetTodaySubmittersAsync_SubmissionWithoutUsername_IsListedAsAnonymous()
    {
        var game = new Game { Id = GameId, Name = "G" };
        var games = new Mock<IGameRepository>();
        games.Setup(g => g.GetByIdWithoutBlobAsync(GameId, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        var visibility = new Mock<IScoreVisibilityService>();
        visibility.Setup(v => v.GetCurrentScoringDay(game)).Returns(CurrentDay);
        var submissions = new Mock<ISubmissionRepository>();
        submissions.Setup(s => s.GetUsernamesForDayAsync(GameId, CurrentDay, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["dora", null]);
        var service = new SubmissionQueryService(
            submissions.Object, games.Object, Mock.Of<IUserSubmissionChecker>(), visibility.Object, Mock.Of<IScreenshotUrlSigner>());

        var submitters = await service.GetTodaySubmittersAsync(GameId, CancellationToken.None);

        Assert.Equal(["dora", DisplayNames.Anonymous], submitters.Usernames);
        Assert.Equal(2, submitters.Count);
    }
}
