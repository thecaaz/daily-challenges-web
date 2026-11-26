using DailyChallenges.Achievements;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services;
using DailyChallenges.Services.Contracts;
using DailyChallenges.Services.Ranking;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using DailyChallenges.TestUtilities;
using Moq;

namespace DailyChallenges.Tests;

/// <summary>
/// Orchestration tests with mocked repositories and a pass-through transaction runner. Behaviour against
/// real data (rollback, idempotency, achievements) is covered in the integration tests (ScoringDayFinalizationTests).
/// </summary>
public class ScoringDayFinalizerServiceTests
{
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
        var clock = new FixedTimeProvider(Day.AddDays(1));

        var results = new Mock<IScoringDayResultRepository>();
        var subs    = new Mock<ISubmissionRepository>();
        var games   = new Mock<IGameRepository>();
        var notifs  = new Mock<INotificationRepository>();
        var xp      = new Mock<IXpService>();
        var ach     = new Mock<IAchievementService>();
        var transactions = new Mock<ITransactionRunner>();

        results.Setup(r => r.TryCreateAsync(It.IsAny<ScoringDayResult>())).ReturnsAsync(true);
        notifs.Setup(r => r.CreateBatchAsync(It.IsAny<List<Notification>>()))
              .Returns(Task.CompletedTask);
        xp.Setup(r => r.AwardForDayWinAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(config.WinXp);
        transactions.Setup(t => t.RunAsync(It.IsAny<Func<Task<ScoringDayOutcome?>>>()))
                    .Returns((Func<Task<ScoringDayOutcome?>> work) => work());

        var recorder = new ScoringDayOutcomeRecorder(
            subs.Object, results.Object, xp.Object, notifs.Object,
            new ScoringDayNotificationFactory(Options.Create(config), clock),
            clock,
            NullLogger<ScoringDayOutcomeRecorder>.Instance);
        var svc = new ScoringDayFinalizerService(
            results.Object, games.Object, recorder, transactions.Object, ach.Object,
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

        await sut.Service.FinalizeScoringDayAsync(1, Day, CancellationToken.None);

        sut.GamesRepo.Verify(r => r.GetByIdWithoutBlobAsync(It.IsAny<int>()), Times.Never);
        sut.XpService.Verify(r => r.AwardForDayWinAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        sut.ResultsRepo.Verify(r => r.TryCreateAsync(It.IsAny<ScoringDayResult>()), Times.Never);
    }

    // ── Game not found ────────────────────────────────────────────────────────

    [Fact]
    public async Task FinalizeScoringDay_GameNotFound_DoesNotCreateResult()
    {
        var sut = Build();
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdWithoutBlobAsync(1)).ReturnsAsync((Game?)null);

        await sut.Service.FinalizeScoringDayAsync(1, Day, CancellationToken.None);

        sut.ResultsRepo.Verify(r => r.TryCreateAsync(It.IsAny<ScoringDayResult>()), Times.Never);
    }

    // ── No scored submissions ─────────────────────────────────────────────────

    [Fact]
    public async Task FinalizeScoringDay_NoSubmissions_RecordsResultWithNullWinner()
    {
        var sut = Build();
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdWithoutBlobAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(new List<Submission>());

        await sut.Service.FinalizeScoringDayAsync(1, Day, CancellationToken.None);

        sut.ResultsRepo.Verify(r => r.TryCreateAsync(It.Is<ScoringDayResult>(
            r => r.WinnerUserId == null && r.GameId == 1)), Times.Once);
        sut.XpService.Verify(r => r.AwardForDayWinAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FinalizeScoringDay_NoSubmissions_DoesNotCreateNotifications()
    {
        var sut = Build();
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdWithoutBlobAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(new List<Submission>());

        await sut.Service.FinalizeScoringDayAsync(1, Day, CancellationToken.None);

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
        sut.GamesRepo.Setup(r => r.GetByIdWithoutBlobAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(new List<Submission> { winner, second });

        await sut.Service.FinalizeScoringDayAsync(1, Day, CancellationToken.None);

        sut.XpService.Verify(r => r.AwardForDayWinAsync(10, 1, Day.Date, It.IsAny<CancellationToken>()), Times.Once);
        sut.XpService.Verify(r => r.AwardForDayWinAsync(11, It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FinalizeScoringDay_WithWinner_RecordsResultWithWinnerUserId()
    {
        var sut = Build();
        var winner = MakeSub(1, userId: 10);
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdWithoutBlobAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(new List<Submission> { winner });

        await sut.Service.FinalizeScoringDayAsync(1, Day, CancellationToken.None);

        sut.ResultsRepo.Verify(r => r.TryCreateAsync(It.Is<ScoringDayResult>(
            r => r.WinnerUserId == 10 && r.GameId == 1)), Times.Once);
    }

    [Fact]
    public async Task FinalizeScoringDay_WithWinner_TriggersAchievementDayWin()
    {
        var sut = Build();
        var winner = MakeSub(1, userId: 10);
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdWithoutBlobAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(new List<Submission> { winner });

        await sut.Service.FinalizeScoringDayAsync(1, Day, CancellationToken.None);

        sut.Achievements.Verify(a => a.CheckAndAwardAsync(10, AchievementTrigger.DayWin), Times.Once);
        sut.Achievements.Verify(a => a.CheckAndAwardAsync(10, AchievementTrigger.LevelUp), Times.Once);
    }

    /// <summary>H6: the win must be recorded before the DayWin achievement check counts wins.</summary>
    [Fact]
    public async Task FinalizeScoringDay_AchievementsRunAfterResultAndXp()
    {
        var sut = Build();
        var calls = new List<string>();
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdWithoutBlobAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(new List<Submission> { MakeSub(1, userId: 10) });
        sut.ResultsRepo.Setup(r => r.TryCreateAsync(It.IsAny<ScoringDayResult>()))
            .Callback(() => calls.Add("result")).ReturnsAsync(true);
        sut.XpService.Setup(x => x.AwardForDayWinAsync(10, 1, Day.Date, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("xp")).ReturnsAsync(100);
        sut.Achievements.Setup(a => a.CheckAndAwardAsync(10, It.IsAny<AchievementTrigger>()))
            .Callback((int _, AchievementTrigger t) => calls.Add(t.ToString())).Returns(Task.CompletedTask);

        await sut.Service.FinalizeScoringDayAsync(1, Day, CancellationToken.None);

        Assert.Equal(new[] { "result", "xp", "DayWin", "LevelUp" }, calls);
    }

    /// <summary>The claim is lost when another run inserted the ScoringDayResult between the pre-check and the claim.</summary>
    [Fact]
    public async Task FinalizeScoringDay_LostClaim_DoesNotAwardXpOrNotify()
    {
        var sut = Build();
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdWithoutBlobAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(new List<Submission> { MakeSub(1, userId: 10) });
        sut.ResultsRepo.Setup(r => r.TryCreateAsync(It.IsAny<ScoringDayResult>())).ReturnsAsync(false);

        await sut.Service.FinalizeScoringDayAsync(1, Day, CancellationToken.None);

        sut.XpService.Verify(x => x.AwardForDayWinAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        sut.NotifRepo.Verify(r => r.CreateBatchAsync(It.IsAny<List<Notification>>()), Times.Never);
        sut.Achievements.Verify(a => a.CheckAndAwardAsync(It.IsAny<int>(), It.IsAny<AchievementTrigger>()), Times.Never);
    }

    [Fact]
    public async Task FinalizeScoringDay_FailureMidway_RethrowsAndSkipsAchievements()
    {
        var sut = Build();
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdWithoutBlobAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(new List<Submission> { MakeSub(1, userId: 10) });
        sut.NotifRepo.Setup(r => r.CreateBatchAsync(It.IsAny<List<Notification>>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Service.FinalizeScoringDayAsync(1, Day, CancellationToken.None));

        sut.Achievements.Verify(a => a.CheckAndAwardAsync(It.IsAny<int>(), It.IsAny<AchievementTrigger>()), Times.Never);
    }

    // ── Notifications ─────────────────────────────────────────────────────────

    [Fact]
    public async Task FinalizeScoringDay_WithParticipants_NotifiesOnlyRegisteredUsers()
    {
        var sut = Build();
        var winner = MakeSub(1, userId: 10, scoreValue: 200);
        var runnerUp = MakeSub(2, userId: 11, scoreValue: 100);
        var anonymous = MakeSub(3, userId: null, scoreValue: 50);
        var subs = new List<Submission> { winner, runnerUp, anonymous };

        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdWithoutBlobAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(subs);

        List<Notification>? capturedNotifs = null;
        sut.NotifRepo.Setup(r => r.CreateBatchAsync(It.IsAny<List<Notification>>()))
            .Callback<List<Notification>>(n => capturedNotifs = n)
            .Returns(Task.CompletedTask);

        await sut.Service.FinalizeScoringDayAsync(1, Day, CancellationToken.None);

        Assert.NotNull(capturedNotifs);
        Assert.Equal(2, capturedNotifs!.Count);
    }

    [Fact]
    public async Task FinalizeScoringDay_Winner_ReceivesDayWinNotification()
    {
        var sut = Build();
        var winner = MakeSub(1, userId: 10, scoreValue: 200);
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdWithoutBlobAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(new List<Submission> { winner });

        List<Notification>? notifs = null;
        sut.NotifRepo.Setup(r => r.CreateBatchAsync(It.IsAny<List<Notification>>()))
            .Callback<List<Notification>>(n => notifs = n)
            .Returns(Task.CompletedTask);

        await sut.Service.FinalizeScoringDayAsync(1, Day, CancellationToken.None);

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
        sut.GamesRepo.Setup(r => r.GetByIdWithoutBlobAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(subs);

        List<Notification>? notifs = null;
        sut.NotifRepo.Setup(r => r.CreateBatchAsync(It.IsAny<List<Notification>>()))
            .Callback<List<Notification>>(n => notifs = n)
            .Returns(Task.CompletedTask);

        await sut.Service.FinalizeScoringDayAsync(1, Day, CancellationToken.None);

        Assert.Contains(notifs!, n => n.UserId == 11 && n.Type == "day_placement" && n.Rank == 2);
    }

    // ── Anonymous winner ──────────────────────────────────────────────────────

    [Fact]
    public async Task FinalizeScoringDay_AnonymousWinner_DoesNotAwardXp()
    {
        var sut = Build();
        var anonymousSubmission = MakeSub(1, userId: null, scoreValue: 500);
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdWithoutBlobAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(new List<Submission> { anonymousSubmission });

        await sut.Service.FinalizeScoringDayAsync(1, Day, CancellationToken.None);

        sut.XpService.Verify(r => r.AwardForDayWinAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FinalizeScoringDay_AnonymousWinner_RecordsResultWithNullWinnerId()
    {
        var sut = Build();
        var anonymousSubmission = MakeSub(1, userId: null);
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdWithoutBlobAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(new List<Submission> { anonymousSubmission });

        await sut.Service.FinalizeScoringDayAsync(1, Day, CancellationToken.None);

        sut.ResultsRepo.Verify(r => r.TryCreateAsync(It.Is<ScoringDayResult>(
            r => r.WinnerUserId == null)), Times.Once);
    }

    [Fact]
    public async Task FinalizeScoringDay_Notifications_CarryMessageDayAndClock()
    {
        var sut = Build(cfg => cfg.WinXp = 123);
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdWithoutBlobAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(new List<Submission> { MakeSub(1, userId: 10, scoreValue: 200), MakeSub(2, userId: null, scoreValue: 150), MakeSub(3, userId: 12, scoreValue: 100) });
        List<Notification>? notifs = null;
        sut.NotifRepo.Setup(r => r.CreateBatchAsync(It.IsAny<List<Notification>>()))
            .Callback<List<Notification>>(n => notifs = n)
            .Returns(Task.CompletedTask);

        await sut.Service.FinalizeScoringDayAsync(1, Day.AddHours(15), CancellationToken.None);

        Assert.Equal(new[] { "You won on TestGame! +123 XP", "You placed #3 on TestGame" }, notifs!.Select(n => n.Message));
        Assert.Equal(new int?[] { 1, 3 }, notifs!.Select(n => n.Rank));
        Assert.All(notifs!, n =>
        {
            Assert.Equal(1, n.GameId);
            Assert.Equal(Day.Date, n.ScoringDay);
            Assert.Equal(Day.AddDays(1), n.CreatedAt);
            Assert.False(n.IsRead);
        });
    }

    [Fact]
    public async Task FinalizeScoringDay_RecordsResultAtTheClocksTimeForTheDatePart()
    {
        var sut = Build();
        sut.ResultsRepo.Setup(r => r.ExistsAsync(1, Day.Date)).ReturnsAsync(false);
        sut.GamesRepo.Setup(r => r.GetByIdWithoutBlobAsync(1)).ReturnsAsync(MakeGame());
        sut.SubsRepo.Setup(r => r.GetByGameAndDayByScoreValueAsync(1, Day.Date, It.IsAny<IRankingStrategy>()))
            .ReturnsAsync(new List<Submission>());

        await sut.Service.FinalizeScoringDayAsync(1, Day.AddHours(15), CancellationToken.None);

        sut.ResultsRepo.Verify(r => r.TryCreateAsync(It.Is<ScoringDayResult>(
            r => r.ScoringDay == Day.Date && r.ProcessedAt == Day.AddDays(1))), Times.Once);
    }
}

