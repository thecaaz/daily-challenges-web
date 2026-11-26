using System.Security.Claims;
using DailyChallenges.DTOs;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services;
using DailyChallenges.TestUtilities;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace DailyChallenges.Tests;

public class UserProfileServiceTests
{
    private const int OwnerId = 5;
    private const int GameA = 1;
    private const int GameB = 2;
    private static readonly DateTime Now = new(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Today = Now.Date;

    private sealed record Sut(
        UserProfileService Service,
        Mock<IUserProfileRepository> Profiles,
        Mock<ISubmissionRepository> Submissions,
        List<List<DateTime>> RequestedDays,
        List<IReadOnlyCollection<int>?> ExcludedIds);

    private static Sut Build(List<Submission> submissions)
    {
        var profiles = new Mock<IUserProfileRepository>();
        var submissionRepo = new Mock<ISubmissionRepository>();
        var games = new Mock<IGameRepository>();
        var requestedDays = new List<List<DateTime>>();
        var excludedIds = new List<IReadOnlyCollection<int>?>();

        profiles.Setup(r => r.GetByIdAsync(OwnerId, It.IsAny<CancellationToken>())).ReturnsAsync(new User { Id = OwnerId, Username = "owner" });
        profiles.Setup(r => r.GetMostPlayedGamesAsync(OwnerId, It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()))
            .Callback<int, int, IReadOnlyCollection<int>?, CancellationToken>((_, _, excluded, _) => excludedIds.Add(excluded))
            .ReturnsAsync(() => new List<UserGameStatDto>
            {
                new() { GameId = GameA, Name = "A", Plays = 3 },
                new() { GameId = GameB, Name = "B", Plays = 1 }
            });
        games.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Game>
        {
            new() { Id = GameA, Name = "A", ResetTime = TimeSpan.Zero },
            new() { Id = GameB, Name = "B", ResetTime = TimeSpan.Zero }
        });
        submissionRepo.Setup(r => r.GetByUserAndGamesAndDaysAsync(OwnerId, It.IsAny<List<int>>(), It.IsAny<List<DateTime>>(), It.IsAny<CancellationToken>()))
            .Callback<int, List<int>, List<DateTime>, CancellationToken>((_, _, days, _) => requestedDays.Add(days))
            .ReturnsAsync(submissions);

        var service = new UserProfileService(profiles.Object, submissionRepo.Object, games.Object,
            new LevelCalculator(), new MemoryCache(new MemoryCacheOptions()), new FixedTimeProvider(Now));
        return new Sut(service, profiles, submissionRepo, requestedDays, excludedIds);
    }

    private static Submission On(int id, int gameId, DateTime day, double score) =>
        new() { Id = id, GameId = gameId, UserId = OwnerId, ScoringDay = day, ScoreValue = score, Score = score.ToString() };

    private static ClaimsPrincipal Viewer(int userId) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test"));

    private static List<Submission> History() => new()
    {
        On(1, GameA, Today, 10),
        On(2, GameA, Today.AddDays(-29), 20),
        On(3, GameA, Today.AddDays(-30), 30),
        On(4, GameA, Today.AddDays(-1), 40),
        On(5, GameB, Today.AddDays(-2), 50)
    };

    [Fact]
    public async Task GetProfileAsync_for_the_owner_builds_a_30_day_sparkline_per_game_in_day_order()
    {
        var sut = Build(History());

        var profile = await sut.Service.GetProfileAsync(OwnerId, 10, Viewer(OwnerId), CancellationToken.None);

        Assert.NotNull(profile);
        Assert.False(profile!.ScoresHidden);
        var gameA = profile.TopGames.Single(g => g.GameId == GameA);
        Assert.Equal(new[] { "2026-05-17", "2026-06-14", "2026-06-15" }, gameA.ScoreHistory.Select(h => h.ScoringDay));
        Assert.Equal(new double?[] { 20, 40, 10 }, gameA.ScoreHistory.Select(h => h.ScoreValue));
        Assert.Equal(new[] { "2026-06-13" }, profile.TopGames.Single(g => g.GameId == GameB).ScoreHistory.Select(h => h.ScoringDay));
        var days = Assert.Single(sut.RequestedDays);
        Assert.Equal(30, days.Count);
        Assert.Equal(Today.AddDays(-29), days.First());
        Assert.Equal(Today, days.Last());
        Assert.Empty(Assert.Single(sut.ExcludedIds)!);
    }

    [Fact]
    public async Task GetProfileAsync_for_another_viewer_hides_current_day_submissions()
    {
        var sut = Build(History());

        var profile = await sut.Service.GetProfileAsync(OwnerId, 10, Viewer(99), CancellationToken.None);

        Assert.True(profile!.ScoresHidden);
        Assert.Equal(new[] { 1 }, Assert.Single(sut.ExcludedIds)!);
        Assert.DoesNotContain("2026-06-15", profile.TopGames.Single(g => g.GameId == GameA).ScoreHistory.Select(h => h.ScoringDay));
        Assert.Equal(new[] { Today }, sut.RequestedDays[0]);
    }

    [Fact]
    public async Task GetProfileAsync_for_another_viewer_without_current_day_submissions_does_not_hide_scores()
    {
        var sut = Build(new List<Submission> { On(4, GameA, Today.AddDays(-1), 40) });

        var profile = await sut.Service.GetProfileAsync(OwnerId, 10, viewer: null, CancellationToken.None);

        Assert.False(profile!.ScoresHidden);
    }

    [Fact]
    public async Task GetProfileAsync_caches_per_user_size_and_visibility()
    {
        var sut = Build(History());

        await sut.Service.GetProfileAsync(OwnerId, 10, Viewer(OwnerId), CancellationToken.None);
        await sut.Service.GetProfileAsync(OwnerId, 10, Viewer(OwnerId), CancellationToken.None);
        await sut.Service.GetProfileAsync(OwnerId, 10, Viewer(99), CancellationToken.None);
        await sut.Service.GetProfileAsync(OwnerId, 5, Viewer(OwnerId), CancellationToken.None);

        sut.Profiles.Verify(r => r.GetByIdAsync(OwnerId, It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task GetProfileAsync_returns_null_for_an_unknown_user()
    {
        var sut = Build(History());

        Assert.Null(await sut.Service.GetProfileAsync(404, 10, Viewer(OwnerId), CancellationToken.None));
    }
}
