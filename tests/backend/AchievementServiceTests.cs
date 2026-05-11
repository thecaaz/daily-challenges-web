using DailyChallenges.Achievements;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DailyChallenges.Tests;

public class AchievementServiceTests
{
    // ── Builder ───────────────────────────────────────────────────────────────

    private record Sut(
        AchievementService Service,
        Mock<IUserAchievementRepository> AchRepo,
        Mock<IUserRepository> UserRepo,
        Mock<ISubmissionRepository> SubRepo,
        Mock<IScoringDayResultRepository> SdrRepo,
        Mock<INotificationRepository> NotifRepo,
        Mock<IFriendRepository> FriendRepo
    );

    private static Sut Build()
    {
        var achRepo   = new Mock<IUserAchievementRepository>();
        var userRepo  = new Mock<IUserRepository>();
        var subRepo   = new Mock<ISubmissionRepository>();
        var sdrRepo   = new Mock<IScoringDayResultRepository>();
        var notifRepo = new Mock<INotificationRepository>();
        var friendRepo = new Mock<IFriendRepository>();

        // Default: nothing unlocked yet
        achRepo.Setup(r => r.GetUnlockedIdsAsync(It.IsAny<int>())).ReturnsAsync(new List<string>());
        achRepo.Setup(r => r.AddBatchAsync(It.IsAny<List<UserAchievement>>())).Returns(Task.CompletedTask);
        notifRepo.Setup(r => r.CreateBatchAsync(It.IsAny<List<Notification>>())).Returns(Task.CompletedTask);

        var svc = new AchievementService(
            achRepo.Object, userRepo.Object, subRepo.Object,
            sdrRepo.Object, notifRepo.Object, friendRepo.Object,
            NullLogger<AchievementService>.Instance);

        return new Sut(svc, achRepo, userRepo, subRepo, sdrRepo, notifRepo, friendRepo);
    }

    private static User MakeUser(int id = 1, int streak = 0, int level = 1) =>
        new() { Id = id, Username = "u", Streak = streak, Level = level, TotalXp = 0 };

    // ── Idempotency: already-unlocked achievements are skipped ────────────────

    [Fact]
    public async Task CheckAndAward_AlreadyUnlocked_DoesNotAwardAgain()
    {
        var sut = Build();
        sut.AchRepo.Setup(r => r.GetUnlockedIdsAsync(1))
            .ReturnsAsync(new List<string> { "submission_first" });
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
        sut.SubRepo.Setup(r => r.CountByUserAsync(1)).ReturnsAsync(5);

        await sut.Service.CheckAndAwardAsync(1, AchievementTrigger.Submission);

        sut.AchRepo.Verify(r => r.AddBatchAsync(It.IsAny<List<UserAchievement>>()), Times.Never);
    }

    // ── Trigger routing: wrong trigger never awards ───────────────────────────

    [Theory]
    [InlineData(AchievementTrigger.DayWin)]
    [InlineData(AchievementTrigger.LevelUp)]
    [InlineData(AchievementTrigger.FriendAccepted)]
    public async Task CheckAndAward_WrongTriggerForSubmission_NoCandidates(AchievementTrigger trigger)
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser(streak: 100, level: 25));
        sut.SubRepo.Setup(r => r.CountByUserAsync(1)).ReturnsAsync(250);

        await sut.Service.CheckAndAwardAsync(1, trigger);

        // Submission achievements should not be evaluated on non-submission triggers
        sut.SubRepo.Verify(r => r.CountByUserAsync(It.IsAny<int>()), Times.Never);
    }

    // ── submission_first ──────────────────────────────────────────────────────

    [Fact]
    public async Task CheckAndAward_Submission_FirstSubmission_Unlocks()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
        sut.SubRepo.Setup(r => r.CountByUserAsync(1)).ReturnsAsync(1);

        await sut.Service.CheckAndAwardAsync(1, AchievementTrigger.Submission);

        sut.AchRepo.Verify(r => r.AddBatchAsync(It.Is<List<UserAchievement>>(
            l => l.Any(a => a.AchievementId == "submission_first"))), Times.Once);
    }

    [Fact]
    public async Task CheckAndAward_Submission_ZeroSubmissions_NoUnlock()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
        sut.SubRepo.Setup(r => r.CountByUserAsync(1)).ReturnsAsync(0);

        await sut.Service.CheckAndAwardAsync(1, AchievementTrigger.Submission);

        sut.AchRepo.Verify(r => r.AddBatchAsync(It.IsAny<List<UserAchievement>>()), Times.Never);
    }

    // ── submission_50 / submission_250 ────────────────────────────────────────

    [Theory]
    [InlineData(50, "submission_50")]
    [InlineData(250, "submission_250")]
    public async Task CheckAndAward_Submission_CountThreshold_Unlocks(int count, string achievementId)
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
        sut.SubRepo.Setup(r => r.CountByUserAsync(1)).ReturnsAsync(count);

        await sut.Service.CheckAndAwardAsync(1, AchievementTrigger.Submission);

        sut.AchRepo.Verify(r => r.AddBatchAsync(It.Is<List<UserAchievement>>(
            l => l.Any(a => a.AchievementId == achievementId))), Times.Once);
    }

    // ── Streak achievements ───────────────────────────────────────────────────

    [Theory]
    [InlineData(7,   "streak_7")]
    [InlineData(30,  "streak_30")]
    [InlineData(100, "streak_100")]
    public async Task CheckAndAward_Submission_StreakThreshold_Unlocks(int streak, string achievementId)
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser(streak: streak));
        sut.SubRepo.Setup(r => r.CountByUserAsync(1)).ReturnsAsync(1);

        await sut.Service.CheckAndAwardAsync(1, AchievementTrigger.Submission);

        sut.AchRepo.Verify(r => r.AddBatchAsync(It.Is<List<UserAchievement>>(
            l => l.Any(a => a.AchievementId == achievementId))), Times.Once);
    }

    [Fact]
    public async Task CheckAndAward_Submission_StreakBelowThreshold_NoStreakUnlock()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser(streak: 6));
        sut.SubRepo.Setup(r => r.CountByUserAsync(1)).ReturnsAsync(1);

        await sut.Service.CheckAndAwardAsync(1, AchievementTrigger.Submission);

        sut.AchRepo.Verify(r => r.AddBatchAsync(It.Is<List<UserAchievement>>(
            l => l.Any(a => a.AchievementId.StartsWith("streak_")))), Times.Never);
    }

    // ── Win achievements ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(1,  "win_1")]
    [InlineData(10, "win_10")]
    [InlineData(50, "win_50")]
    public async Task CheckAndAward_DayWin_WinCountThreshold_Unlocks(int wins, string achievementId)
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
        sut.SdrRepo.Setup(r => r.CountWinsByUserAsync(1)).ReturnsAsync(wins);

        await sut.Service.CheckAndAwardAsync(1, AchievementTrigger.DayWin);

        sut.AchRepo.Verify(r => r.AddBatchAsync(It.Is<List<UserAchievement>>(
            l => l.Any(a => a.AchievementId == achievementId))), Times.Once);
    }

    [Fact]
    public async Task CheckAndAward_DayWin_ZeroWins_NoUnlock()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
        sut.SdrRepo.Setup(r => r.CountWinsByUserAsync(1)).ReturnsAsync(0);

        await sut.Service.CheckAndAwardAsync(1, AchievementTrigger.DayWin);

        sut.AchRepo.Verify(r => r.AddBatchAsync(It.IsAny<List<UserAchievement>>()), Times.Never);
    }

    // ── Level achievements ────────────────────────────────────────────────────

    [Theory]
    [InlineData(5,  "level_5")]
    [InlineData(10, "level_10")]
    [InlineData(25, "level_25")]
    public async Task CheckAndAward_LevelUp_LevelThreshold_Unlocks(int level, string achievementId)
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser(level: level));

        await sut.Service.CheckAndAwardAsync(1, AchievementTrigger.LevelUp);

        sut.AchRepo.Verify(r => r.AddBatchAsync(It.Is<List<UserAchievement>>(
            l => l.Any(a => a.AchievementId == achievementId))), Times.Once);
    }

    [Fact]
    public async Task CheckAndAward_LevelUp_Level4_NoUnlock()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser(level: 4));

        await sut.Service.CheckAndAwardAsync(1, AchievementTrigger.LevelUp);

        sut.AchRepo.Verify(r => r.AddBatchAsync(It.IsAny<List<UserAchievement>>()), Times.Never);
    }

    // ── first_friend ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckAndAward_FriendAccepted_HasFriend_Unlocks()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
        sut.FriendRepo.Setup(r => r.HasAcceptedFriendAsync(1)).ReturnsAsync(true);

        await sut.Service.CheckAndAwardAsync(1, AchievementTrigger.FriendAccepted);

        sut.AchRepo.Verify(r => r.AddBatchAsync(It.Is<List<UserAchievement>>(
            l => l.Any(a => a.AchievementId == "first_friend"))), Times.Once);
    }

    [Fact]
    public async Task CheckAndAward_FriendAccepted_NoFriend_NoUnlock()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
        sut.FriendRepo.Setup(r => r.HasAcceptedFriendAsync(1)).ReturnsAsync(false);

        await sut.Service.CheckAndAwardAsync(1, AchievementTrigger.FriendAccepted);

        sut.AchRepo.Verify(r => r.AddBatchAsync(It.IsAny<List<UserAchievement>>()), Times.Never);
    }

    // ── Notifications created for each newly unlocked achievement ────────────

    [Fact]
    public async Task CheckAndAward_NewUnlock_CreatesNotification()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
        sut.SubRepo.Setup(r => r.CountByUserAsync(1)).ReturnsAsync(1);

        List<Notification>? capturedNotifs = null;
        sut.NotifRepo.Setup(r => r.CreateBatchAsync(It.IsAny<List<Notification>>()))
            .Callback<List<Notification>>(n => capturedNotifs = n)
            .Returns(Task.CompletedTask);

        await sut.Service.CheckAndAwardAsync(1, AchievementTrigger.Submission);

        Assert.NotNull(capturedNotifs);
        Assert.Contains(capturedNotifs!, n => n.Type == "achievement" && n.Message.Contains("First Step"));
    }

    // ── User not found: no crash, no unlock ───────────────────────────────────

    [Fact]
    public async Task CheckAndAward_UserNotFound_DoesNotThrowOrUnlock()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((User?)null);

        // Should not throw (errors are swallowed by the outer try/catch)
        await sut.Service.CheckAndAwardAsync(1, AchievementTrigger.Submission);

        sut.AchRepo.Verify(r => r.AddBatchAsync(It.IsAny<List<UserAchievement>>()), Times.Never);
    }

    // ── GetForUserAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetForUserAsync_ReturnsAllCatalogEntries()
    {
        var sut = Build();
        sut.AchRepo.Setup(r => r.GetUnlockedWithTimestampsAsync(1))
            .ReturnsAsync(new Dictionary<string, DateTime>());

        var result = await sut.Service.GetForUserAsync(1);

        Assert.Equal(AchievementCatalog.All.Count, result.Count);
    }

    [Fact]
    public async Task GetForUserAsync_UnlockedEntry_HasUnlockedAt()
    {
        var sut = Build();
        var ts = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        sut.AchRepo.Setup(r => r.GetUnlockedWithTimestampsAsync(1))
            .ReturnsAsync(new Dictionary<string, DateTime> { ["win_1"] = ts });

        var result = await sut.Service.GetForUserAsync(1);

        var win1 = result.Single(r => r.AchievementId == "win_1");
        Assert.Equal(ts, win1.UnlockedAt);
    }

    [Fact]
    public async Task GetForUserAsync_LockedEntry_HasNullUnlockedAt()
    {
        var sut = Build();
        sut.AchRepo.Setup(r => r.GetUnlockedWithTimestampsAsync(1))
            .ReturnsAsync(new Dictionary<string, DateTime>());

        var result = await sut.Service.GetForUserAsync(1);

        Assert.All(result, r => Assert.Null(r.UnlockedAt));
    }
}
