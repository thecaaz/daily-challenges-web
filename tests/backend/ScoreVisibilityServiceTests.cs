using System.Security.Claims;
using DailyChallenges.Models;
using DailyChallenges.Services;
using DailyChallenges.TestUtilities;
using Moq;

namespace DailyChallenges.Tests;

public class ScoreVisibilityServiceTests
{
    private static readonly DateTime Now = new(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Today = Now.Date;
    private static readonly Game Game = new() { Id = 7, Name = "G", ResetTime = TimeSpan.Zero };

    private readonly Mock<IUserSubmissionChecker> _checker = new();

    private ScoreVisibilityService Build() => new(_checker.Object, new FixedTimeProvider(Now));

    private static ClaimsPrincipal Principal(int userId, bool isAdmin)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        if (isAdmin) claims.Add(new Claim(ClaimTypes.Role, AuthRoles.Admin));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public void GetCurrentScoringDay_UsesTheGamesResetTimeAndTheClock()
    {
        var lateReset = new Game { Id = 8, ResetTime = TimeSpan.FromHours(11) };

        Assert.Equal(Today, Build().GetCurrentScoringDay(Game));
        Assert.Equal(Today.AddDays(-1), Build().GetCurrentScoringDay(lateReset));
    }

    [Fact]
    public async Task CanSeeDay_PastDay_IsVisibleToAnonymousViewers()
    {
        var canSee = await Build().CanSeeDayAsync((ClaimsPrincipal?)null, Game, Today.AddDays(-1), CancellationToken.None);

        Assert.True(canSee);
        _checker.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CanSeeDay_CurrentDay_IsVisibleToAdminsWithoutASubmission()
    {
        var canSee = await Build().CanSeeDayAsync(Principal(1, isAdmin: true), Game, Today, CancellationToken.None);

        Assert.True(canSee);
        _checker.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CanSeeDay_CurrentDay_DependsOnTheUsersSubmission(bool hasSubmitted)
    {
        _checker.Setup(c => c.HasUserSubmittedForDayAsync(3, Game.Id, Today, It.IsAny<CancellationToken>())).ReturnsAsync(hasSubmitted);

        var canSee = await Build().CanSeeDayAsync(Principal(3, isAdmin: false), Game, Today.AddHours(5), CancellationToken.None);

        Assert.Equal(hasSubmitted, canSee);
    }

    [Fact]
    public async Task CanSeeDay_CurrentDay_AnonymousViewerAsksTheCheckerWithoutAUser()
    {
        _checker.Setup(c => c.HasUserSubmittedForDayAsync(null, Game.Id, Today, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var canSee = await Build().CanSeeDayAsync((ClaimsPrincipal?)null, Game, Today, CancellationToken.None);

        Assert.False(canSee);
        _checker.Verify(c => c.HasUserSubmittedForDayAsync(null, Game.Id, Today, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public async Task CanSeeDay_ResolvedViewer_FollowsTheSameRule(bool isAdmin, bool hasSubmitted, bool expected)
    {
        _checker.Setup(c => c.HasUserSubmittedForDayAsync(3, Game.Id, Today, It.IsAny<CancellationToken>())).ReturnsAsync(hasSubmitted);

        var canSee = await Build().CanSeeDayAsync(new ScoreViewer(3, isAdmin), Game, Today, CancellationToken.None);

        Assert.Equal(expected, canSee);
    }
}

