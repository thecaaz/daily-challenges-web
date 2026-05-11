using DailyChallenges.Achievements;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services;
using DailyChallenges.Services.Contracts;
using Microsoft.Extensions.Options;
using Moq;

namespace DailyChallenges.Tests;

public class XpServiceTests
{
    // ── Test helpers ──────────────────────────────────────────────────────────

    private static readonly DateTime Day1 = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = Day1.AddDays(1);
    private static readonly DateTime Day3 = Day1.AddDays(2);
    private static readonly DateTime Day10 = Day1.AddDays(9);

    private record Sut(
        XpService Service,
        Mock<IUserRepository> UserRepo,
        Mock<ISubmissionRepository> SubRepo,
        Mock<IXpEventRepository> XpEventRepo,
        Mock<IAchievementService> Achievements,
        XpConfig Config
    );

    private static Sut Build(Action<XpConfig>? configure = null)
    {
        var cfg = new XpConfig();
        configure?.Invoke(cfg);

        var userRepo = new Mock<IUserRepository>();
        var subRepo = new Mock<ISubmissionRepository>();
        var xpEventRepo = new Mock<IXpEventRepository>();
        var achievements = new Mock<IAchievementService>();
        var levelCalc = new LevelCalculator(cfg.LevelBase, cfg.LevelExponent);

        xpEventRepo
            .Setup(r => r.AddAsync(It.IsAny<XpEvent>()))
            .ReturnsAsync((XpEvent e) => e);

        var svc = new XpService(
            userRepo.Object, subRepo.Object, xpEventRepo.Object,
            levelCalc, Options.Create(cfg), achievements.Object);

        return new Sut(svc, userRepo, subRepo, xpEventRepo, achievements, cfg);
    }

    private static User NewUser(int id = 1, int totalXp = 0, int streak = 0, DateTime? lastSubmission = null) =>
        new() { Id = id, Username = "user", TotalXp = totalXp, Level = 1, Streak = streak, LastSubmissionAt = lastSubmission };

    private static Submission NewSub(int id, int userId, int gameId = 1) =>
        new() { Id = id, UserId = userId, GameId = gameId, Score = "1", CreatedAt = DateTime.UtcNow };

    // ── AwardForSubmissionAsync ───────────────────────────────────────────────

    [Fact]
    public async Task AwardForSubmission_UserNotFound_Returns0()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((User?)null);

        var result = await sut.Service.AwardForSubmissionAsync(1, 1, Day1);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task AwardForSubmission_FirstSubmission_AwardsBaseXp()
    {
        var sut = Build();
        var user = NewUser();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
        sut.SubRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(NewSub(1, 1));

        var result = await sut.Service.AwardForSubmissionAsync(1, 1, Day1);

        Assert.Equal(sut.Config.BaseXpPerSubmission, result);
    }

    [Fact]
    public async Task AwardForSubmission_FirstSubmission_SetsStreak1()
    {
        var sut = Build();
        var user = NewUser();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
        sut.SubRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(NewSub(1, 1));

        await sut.Service.AwardForSubmissionAsync(1, 1, Day1);

        Assert.Equal(1, user.Streak);
    }

    [Fact]
    public async Task AwardForSubmission_SameScoringDay_StreakUnchanged()
    {
        var sut = Build();
        var user = NewUser(streak: 3, lastSubmission: Day1);
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
        sut.SubRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(NewSub(1, 1));

        await sut.Service.AwardForSubmissionAsync(1, 1, Day1);

        Assert.Equal(3, user.Streak);
    }

    [Fact]
    public async Task AwardForSubmission_ConsecutiveDay_IncrementsStreak()
    {
        var sut = Build();
        var user = NewUser(streak: 2, lastSubmission: Day1);
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
        sut.SubRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(NewSub(1, 1));

        await sut.Service.AwardForSubmissionAsync(1, 1, Day2);

        Assert.Equal(3, user.Streak);
    }

    [Fact]
    public async Task AwardForSubmission_NonConsecutiveDay_ResetsStreakTo1()
    {
        var sut = Build();
        var user = NewUser(streak: 5, lastSubmission: Day1);
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
        sut.SubRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(NewSub(1, 1));

        await sut.Service.AwardForSubmissionAsync(1, 1, Day3); // Day1 → Day3: gap of 2

        Assert.Equal(1, user.Streak);
    }

    [Fact]
    public async Task AwardForSubmission_Streak2_AppliesOneDayBonus()
    {
        var sut = Build();
        var user = NewUser(streak: 1, lastSubmission: Day1);
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
        sut.SubRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(NewSub(1, 1));

        var result = await sut.Service.AwardForSubmissionAsync(1, 1, Day2);

        // streak=2 → bonus = 1 * 0.01 = 1% → xp = round(50 * 1.01) = 51
        int expected = (int)Math.Round(50 * 1.01);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task AwardForSubmission_StreakBonus_CappedAtMaxStreakBonus()
    {
        var sut = Build();
        // streak=31 → bonus would be 30*0.01=0.30 which is exactly the cap
        var user = NewUser(streak: 31, lastSubmission: Day1);
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
        sut.SubRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(NewSub(1, 1));

        var result = await sut.Service.AwardForSubmissionAsync(1, 1, Day2);

        int maxXp = (int)Math.Round(50 * (1 + 0.30));
        Assert.Equal(maxXp, result);
    }

    [Fact]
    public async Task AwardForSubmission_StreakBeyondCap_StillCapped()
    {
        var sut = Build();
        var user = NewUser(streak: 100, lastSubmission: Day1);
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
        sut.SubRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(NewSub(1, 1));

        var result = await sut.Service.AwardForSubmissionAsync(1, 1, Day2);

        int maxXp = (int)Math.Round(50 * (1 + 0.30));
        Assert.Equal(maxXp, result);
    }

    [Fact]
    public async Task AwardForSubmission_UpdatesUserTotalXp()
    {
        var sut = Build();
        var user = NewUser(totalXp: 200);
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
        sut.SubRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(NewSub(1, 1));

        var awarded = await sut.Service.AwardForSubmissionAsync(1, 1, Day1);

        Assert.Equal(200 + awarded, user.TotalXp);
    }

    [Fact]
    public async Task AwardForSubmission_WritesXpEventWithCorrectType()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(NewUser());
        sut.SubRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(NewSub(1, 1));

        XpEvent? captured = null;
        sut.XpEventRepo
            .Setup(r => r.AddAsync(It.IsAny<XpEvent>()))
            .Callback<XpEvent>(e => captured = e)
            .ReturnsAsync((XpEvent e) => e);

        await sut.Service.AwardForSubmissionAsync(1, 1, Day1);

        Assert.NotNull(captured);
        Assert.Equal("submission", captured!.EventType);
        Assert.Equal(1, captured.UserId);
        Assert.Equal(1, captured.SubmissionId);
    }

    [Fact]
    public async Task AwardForSubmission_TriggersLevelUpAchievementCheck()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(NewUser());
        sut.SubRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(NewSub(1, 1));

        await sut.Service.AwardForSubmissionAsync(1, 1, Day1);

        sut.Achievements.Verify(a => a.CheckAndAwardAsync(1, AchievementTrigger.LevelUp), Times.Once);
    }

    [Fact]
    public async Task AwardForSubmission_StampsSubmissionXpAwarded()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(NewUser());
        var sub = NewSub(1, 1);
        sut.SubRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(sub);

        var awarded = await sut.Service.AwardForSubmissionAsync(1, 1, Day1);

        Assert.Equal(awarded, sub.XpAwarded);
    }

    [Fact]
    public async Task AwardForSubmission_NewScoringDay_UpdatesLastSubmissionAt()
    {
        var sut = Build();
        var user = NewUser(lastSubmission: Day1);
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
        sut.SubRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(NewSub(1, 1));

        await sut.Service.AwardForSubmissionAsync(1, 1, Day2);

        Assert.Equal(Day2.Date, user.LastSubmissionAt);
    }

    // ── AdjustXpAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AdjustXp_UserNotFound_ThrowsKeyNotFoundException()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.Service.AdjustXpAsync(99, 100, "test"));
    }

    [Fact]
    public async Task AdjustXp_PositiveDelta_IncreasesTotalXp()
    {
        var sut = Build();
        var user = NewUser(totalXp: 100);
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

        await sut.Service.AdjustXpAsync(1, 50, "bonus");

        Assert.Equal(150, user.TotalXp);
    }

    [Fact]
    public async Task AdjustXp_NegativeDelta_DecreasesTotalXp()
    {
        var sut = Build();
        var user = NewUser(totalXp: 100);
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

        await sut.Service.AdjustXpAsync(1, -30, "penalty");

        Assert.Equal(70, user.TotalXp);
    }

    [Fact]
    public async Task AdjustXp_NegativeDeltaExceedsXp_ClampsToZero()
    {
        var sut = Build();
        var user = NewUser(totalXp: 10);
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

        await sut.Service.AdjustXpAsync(1, -1000, "penalty");

        Assert.Equal(0, user.TotalXp);
    }

    [Fact]
    public async Task AdjustXp_WritesAdminAdjustmentEvent()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(NewUser(totalXp: 100));

        XpEvent? captured = null;
        sut.XpEventRepo
            .Setup(r => r.AddAsync(It.IsAny<XpEvent>()))
            .Callback<XpEvent>(e => captured = e)
            .ReturnsAsync((XpEvent e) => e);

        await sut.Service.AdjustXpAsync(1, 50, "reward", adminUserId: 2);

        Assert.NotNull(captured);
        Assert.Equal("admin_adjustment", captured!.EventType);
        Assert.Contains("admin=2", captured.Details);
    }

    // ── AwardForDayWinAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task AwardForDayWin_UserNotFound_Returns0()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((User?)null);

        var result = await sut.Service.AwardForDayWinAsync(1, 1, Day1);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task AwardForDayWin_AwardsWinXp()
    {
        var sut = Build();
        var user = NewUser(totalXp: 0);
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

        var result = await sut.Service.AwardForDayWinAsync(1, 1, Day1);

        Assert.Equal(sut.Config.WinXp, result);
        Assert.Equal(sut.Config.WinXp, user.TotalXp);
    }

    [Fact]
    public async Task AwardForDayWin_WritesDayWinXpEvent()
    {
        var sut = Build();
        sut.UserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(NewUser());

        XpEvent? captured = null;
        sut.XpEventRepo
            .Setup(r => r.AddAsync(It.IsAny<XpEvent>()))
            .Callback<XpEvent>(e => captured = e)
            .ReturnsAsync((XpEvent e) => e);

        await sut.Service.AwardForDayWinAsync(1, 5, Day1);

        Assert.NotNull(captured);
        Assert.Equal("day_win", captured!.EventType);
        Assert.Equal(5, captured.GameId);
        Assert.Equal(Day1.Date, captured.ScoringDay);
    }
}
