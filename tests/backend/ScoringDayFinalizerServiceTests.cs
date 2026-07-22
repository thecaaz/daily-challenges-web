using DailyChallenges.Achievements;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services;
using DailyChallenges.Services.Contracts;
using DailyChallenges.Services.Ranking;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DailyChallenges.Tests;

public class ScoringDayFinalizerServiceTests
{
    // ── Builder ───────────────────────────────────────────────────────────────

    private record Sut(
        ScoringDayFinalizerService Service,
        Mock<IScoringDayResultRepository> ResultsRepo,
        Mock<ISubmissionRepository> SubsRepo,
        Mock<IGameRepository> GamesRepo,
        Mock<INotificationRepository> NotifRepo,
        Mock<IXpService> XpService,
        Mock<IAchievementService> Achievements
    );

    private static Sut Build(Action<XpConfig>? cfg = null)
    {
        var config = new XpConfig();
        cfg?.Invoke(config);

        var results = new Mock<IScoringDayResultRepository>();
        var subs    = new Mock<ISubmissionRepository>();
        var games   = new Mock<IGameRepository>();
        var notifs  = new Mock<INotificationRepository>();
        var xp      = new Mock<IXpService>();
        var ach     = new Mock<IAchievementService>();

        results.Setup(r => r.CreateAsync(It.IsAny<ScoringDayResult>()))
               .ReturnsAsync((ScoringDayResult r) => r);
        notifs.Setup(r => r.CreateBatchAsync(It.IsAny<List<Notification>>()))
              .Returns(Task.CompletedTask);
        xp.Setup(r => r.AwardForDayWinAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()))
          .ReturnsAsync(config.WinXp);

        var svc = new ScoringDayFinalizerService(
            results.Object, subs.Object, games.Object, notifs.Object,
            xp.Object, ach.Object,
            Options.Create(config),
            NullLogger<ScoringDayFinalizerService>.Instance);

        return new Sut(svc, results, subs, games, notifs, xp, ach);
    }

    private static readonly DateTime Day = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Game MakeGame(int id = 1, RankingMode mode = RankingMode.Highest) =>
        new() { Id = id, Name = "TestGame", ResetTime = TimeSpan.Zero, RankingMode = mode };

    private static Submission MakeSub(int id, int? userId, int gameId = 1, double? scoreValue = 100.0) =>
        new() { Id = id, GameId = gameId, UserId = userId, Score = scoreValue?.ToString() ?? "", ScoreValue = scoreValue, ScoringDay = Day, CreatedAt = DateTime.UtcNow, Username = $"user{userId}" };

    // ── Idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public async Task FinalizeScoringDay_AlreadyFinalized_SkipsAllWork()
    {
        var sut = Build();
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(true);

        await sut.Service.FinalizeScoringDayAsync(1, Day);

        sut.GamesRepo.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        sut.XpService.Verify(r => r.AwardForDayWinAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
        sut.ResultsRepo.Verify(r => r.CreateAsync(It.IsAny<ScoringDayResult>()), Times.Never);
    }

    // ── Game not found ────────────────────────────────────────────────────────

    [Fact]
    public async Task FinalizeScoringDay_GameNotFound_DoesNotCreateResult()
    {
        var sut = Build();
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((Game?)null);

        await sut.Service.FinalizeScoringDayAsync(1, Day);

        sut.ResultsRepo.Verify(r => r.CreateAsync(It.IsAny<ScoringDayResult>()), Times.Never);
    }

    // ── No scored submissions ─────────────────────────────────────────────────

    [Fact]
    public async Task FinalizeScoringDay_NoSubmissions_RecordsResultWithNullWinner()
    {
        var sut = Build();
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(new List<Submission>());

        await sut.Service.FinalizeScoringDayAsync(1, Day);

        sut.ResultsRepo.Verify(r => r.CreateAsync(It.Is<ScoringDayResult>(
            r => r.WinnerUserId == null && r.GameId == 1)), Times.Once);
        sut.XpService.Verify(r => r.AwardForDayWinAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task FinalizeScoringDay_NoSubmissions_DoesNotCreateNotifications()
    {
        var sut = Build();
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(new List<Submission>());

        await sut.Service.FinalizeScoringDayAsync(1, Day);

        sut.NotifRepo.Verify(r => r.CreateBatchAsync(It.IsAny<List<Notification>>()), Times.Never);
    }

    // ── With winner ───────────────────────────────────────────────────────────

    [Fact]
    public async Task FinalizeScoringDay_WithWinner_AwardsWinXpToTopSubmission()
    {
        var sut = Build();
        var winner = MakeSub(1, userId: 10, scoreValue: 200);
        var second = MakeSub(2, userId: 11, scoreValue: 100);

        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(new List<Submission> { winner, second });

        await sut.Service.FinalizeScoringDayAsync(1, Day);

        sut.XpService.Verify(r => r.AwardForDayWinAsync(10, 1, Day.Date), Times.Once);
        sut.XpService.Verify(r => r.AwardForDayWinAsync(11, It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task FinalizeScoringDay_WithWinner_RecordsResultWithWinnerUserId()
    {
        var sut = Build();
        var winner = MakeSub(1, userId: 10);
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(new List<Submission> { winner });

        await sut.Service.FinalizeScoringDayAsync(1, Day);

        sut.ResultsRepo.Verify(r => r.CreateAsync(It.Is<ScoringDayResult>(
            r => r.WinnerUserId == 10 && r.GameId == 1)), Times.Once);
    }

    [Fact]
    public async Task FinalizeScoringDay_WithWinner_TriggersAchievementDayWin()
    {
        var sut = Build();
        var winner = MakeSub(1, userId: 10);
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(new List<Submission> { winner });

        await sut.Service.FinalizeScoringDayAsync(1, Day);

        sut.Achievements.Verify(a => a.CheckAndAwardAsync(10, AchievementTrigger.DayWin), Times.Once);
    }

    // ── Notifications ─────────────────────────────────────────────────────────

    [Fact]
    public async Task FinalizeScoringDay_WithParticipants_CreatesNotificationsForAllRegistered()
    {
        var sut = Build();
        var subs = new List<Submission>
        {
            MakeSub(1, userId: 10, scoreValue: 200), // rank 1 – winner
            MakeSub(2, userId: 11, scoreValue: 100), // rank 2
            MakeSub(3, userId: null, scoreValue: 50) // anonymous – skipped
        };

        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(subs);

        List<Notification>? capturedNotifs = null;
        sut.NotifRepo.Setup(r => r.CreateBatchAsync(It.IsAny<List<Notification>>()))
            .Callback<List<Notification>>(n => capturedNotifs = n)
            .Returns(Task.CompletedTask);

        await sut.Service.FinalizeScoringDayAsync(1, Day);

        Assert.NotNull(capturedNotifs);
        Assert.Equal(2, capturedNotifs!.Count); // anonymous excluded
    }

    [Fact]
    public async Task FinalizeScoringDay_Winner_ReceivesDayWinNotification()
    {
        var sut = Build();
        var winner = MakeSub(1, userId: 10, scoreValue: 200);
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(new List<Submission> { winner });

        List<Notification>? notifs = null;
        sut.NotifRepo.Setup(r => r.CreateBatchAsync(It.IsAny<List<Notification>>()))
            .Callback<List<Notification>>(n => notifs = n)
            .Returns(Task.CompletedTask);

        await sut.Service.FinalizeScoringDayAsync(1, Day);

        Assert.Contains(notifs!, n => n.UserId == 10 && n.Type == "day_win" && n.Rank == 1);
    }

    [Fact]
    public async Task FinalizeScoringDay_RunnerUp_ReceivesDayPlacementNotification()
    {
        var sut = Build();
        var subs = new List<Submission>
        {
            MakeSub(1, userId: 10, scoreValue: 200),
            MakeSub(2, userId: 11, scoreValue: 100)
        };
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(subs);

        List<Notification>? notifs = null;
        sut.NotifRepo.Setup(r => r.CreateBatchAsync(It.IsAny<List<Notification>>()))
            .Callback<List<Notification>>(n => notifs = n)
            .Returns(Task.CompletedTask);

        await sut.Service.FinalizeScoringDayAsync(1, Day);

        Assert.Contains(notifs!, n => n.UserId == 11 && n.Type == "day_placement" && n.Rank == 2);
    }

    // ── Anonymous winner ──────────────────────────────────────────────────────

    [Fact]
    public async Task FinalizeScoringDay_AnonymousWinner_DoesNotAwardXp()
    {
        var sut = Build();
        var anonSub = MakeSub(1, userId: null, scoreValue: 500); // anonymous top score
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(new List<Submission> { anonSub });

        await sut.Service.FinalizeScoringDayAsync(1, Day);

        sut.XpService.Verify(r => r.AwardForDayWinAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task FinalizeScoringDay_AnonymousWinner_RecordsResultWithNullWinnerId()
    {
        var sut = Build();
        var anonSub = MakeSub(1, userId: null);
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(new List<Submission> { anonSub });

        await sut.Service.FinalizeScoringDayAsync(1, Day);

        sut.ResultsRepo.Verify(r => r.CreateAsync(It.Is<ScoringDayResult>(
            r => r.WinnerUserId == null)), Times.Once);
    }
}
