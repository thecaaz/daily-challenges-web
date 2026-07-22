using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Services;

namespace DailyChallenges.Tests;

public class DtoMapperTests
{
    private static LevelCalculator DefaultCalc() => new();

    private static readonly DateTime BaseTime = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

    // ── User → UserDto ────────────────────────────────────────────────────────

    [Fact]
    public void ToDto_User_MapsScalarFields()
    {
        var user = new User { Id = 7, Username = "alice", IsAdmin = true, TotalXp = 0, Level = 1, Streak = 5, LastSubmissionAt = BaseTime };
        var dto = DtoMapper.ToDto(user, DefaultCalc());

        Assert.Equal(7, dto.Id);
        Assert.Equal("alice", dto.Username);
        Assert.True(dto.IsAdmin);
        Assert.Equal(5, dto.Streak);
        Assert.Equal(BaseTime, dto.LastSubmissionAt);
    }

    [Fact]
    public void ToDto_User_Level1_XpToNextLevelIs100()
    {
        var user = new User { TotalXp = 0, Level = 1 };
        var dto = DtoMapper.ToDto(user, DefaultCalc());

        Assert.Equal(1, dto.Level);
        Assert.Equal(0, dto.XpIntoLevel);
        Assert.Equal(100, dto.XpToNextLevel);
    }

    [Fact]
    public void ToDto_User_TotalXpReflectedInDto()
    {
        var user = new User { TotalXp = 500, Level = 1 };
        var dto = DtoMapper.ToDto(user, DefaultCalc());
        Assert.Equal(500, dto.TotalXp);
    }

    // ── Submission → SubmissionDto ────────────────────────────────────────────

    [Fact]
    public void ToDto_Submission_MapsScalarFields()
    {
        var sub = new Submission
        {
            Id = 42, GameId = 1, UserId = 3, Score = "3/6",
            ScoreValue = 3.0, Username = "bob", CreatedAt = BaseTime,
            ScreenshotData = null
        };
        var dto = DtoMapper.ToDto(sub);

        Assert.Equal(42, dto.Id);
        Assert.Equal(1, dto.GameId);
        Assert.Equal(3, dto.UserId);
        Assert.Equal("3/6", dto.Score);
        Assert.Equal(3.0, dto.ScoreValue);
        Assert.Equal("bob", dto.Username);
        Assert.Equal(BaseTime, dto.CreatedAt);
        Assert.Null(dto.ScreenshotUrl);
    }

    [Fact]
    public void ToDto_Submission_WithScreenshotData_SetsScreenshotUrl()
    {
        var sub = new Submission { Id = 99, GameId = 1, UserId = 1, Score = "1", ScreenshotData = [0xFF, 0xD8] };
        var dto = DtoMapper.ToDto(sub);
        Assert.Equal("/api/submissions/99/screenshot", dto.ScreenshotUrl);
    }

    [Fact]
    public void ToDto_Submission_RankIsNullByDefault()
    {
        var sub = new Submission { Id = 1, GameId = 1, UserId = 1, Score = "1" };
        var dto = DtoMapper.ToDto(sub);
        Assert.Null(dto.Rank);
    }

    // ── Game → GameDto ────────────────────────────────────────────────────────

    [Fact]
    public void ToDto_Game_MapsScalarFields()
    {
        var game = new Game
        {
            Id = 5, Name = "Wordle", Url = "https://wordle.game",
            Description = "Classic word game", ResetTime = TimeSpan.Zero,
            RankingMode = RankingMode.Lowest, ScreenshotData = null
        };
        var dto = DtoMapper.ToDto(game);

        Assert.Equal(5, dto.Id);
        Assert.Equal("Wordle", dto.Name);
        Assert.Equal("https://wordle.game", dto.Url);
        Assert.Equal("Classic word game", dto.Description);
        Assert.Equal("lowest", dto.RankingMode);
        Assert.Null(dto.ImageUrl);
    }

    [Fact]
    public void ToDto_Game_WithImageData_SetsImageUrl()
    {
        var game = new Game { Id = 3, Name = "G", ResetTime = TimeSpan.Zero, RankingMode = RankingMode.Highest, ScreenshotData = [0x89, 0x50] };
        var dto = DtoMapper.ToDto(game);
        Assert.Equal("/api/games/3/image", dto.ImageUrl);
    }

    [Fact]
    public void ToDto_Game_IncludeSubmissionsFalse_SubmissionsIsNull()
    {
        var game = new Game
        {
            Id = 1, Name = "G", ResetTime = TimeSpan.Zero, RankingMode = RankingMode.Highest,
            Submissions = [new Submission { Id = 1, GameId = 1, UserId = 1, Score = "1" }]
        };
        var dto = DtoMapper.ToDto(game, includeSubmissions: false);
        Assert.Null(dto.Submissions);
    }

    [Fact]
    public void ToDto_Game_IncludeSubmissionsTrue_SubmissionsMapped()
    {
        var game = new Game
        {
            Id = 1, Name = "G", ResetTime = TimeSpan.Zero, RankingMode = RankingMode.Highest,
            Submissions = [new Submission { Id = 77, GameId = 1, UserId = 1, Score = "5" }]
        };
        var dto = DtoMapper.ToDto(game, includeSubmissions: true);
        Assert.NotNull(dto.Submissions);
        Assert.Single(dto.Submissions);
        Assert.Equal(77, dto.Submissions![0].Id);
    }

    // ── XpEvent → XpEventDto ─────────────────────────────────────────────────

    [Fact]
    public void ToDto_XpEvent_MapsAllFields()
    {
        var ev = new XpEvent
        {
            Id = 10, UserId = 2, SubmissionId = 5, GameId = 3,
            ScoringDay = BaseTime.Date, Amount = 55,
            EventType = "submission", Details = "streak=2,bonus=1%",
            CreatedAt = BaseTime
        };
        var dto = DtoMapper.ToDto(ev);

        Assert.Equal(10, dto.Id);
        Assert.Equal(2, dto.UserId);
        Assert.Equal(5, dto.SubmissionId);
        Assert.Equal(3, dto.GameId);
        Assert.Equal(BaseTime.Date, dto.ScoringDay);
        Assert.Equal(55, dto.Amount);
        Assert.Equal("submission", dto.EventType);
        Assert.Equal("streak=2,bonus=1%", dto.Details);
        Assert.Equal(BaseTime, dto.CreatedAt);
    }

    // ── Notification → NotificationDto ───────────────────────────────────────

    [Fact]
    public void ToDto_Notification_MapsAllFields()
    {
        var n = new Notification
        {
            Id = 20, UserId = 3, GameId = 4, Message = "You won!",
            Type = "day_win", ScoringDay = BaseTime.Date,
            Rank = 1, IsRead = false, CreatedAt = BaseTime
        };
        var dto = DtoMapper.ToDto(n);

        Assert.Equal(20, dto.Id);
        Assert.Equal("You won!", dto.Message);
        Assert.Equal("day_win", dto.Type);
        Assert.Equal(1, dto.Rank);
        Assert.False(dto.IsRead);
        Assert.Equal(BaseTime, dto.CreatedAt);
    }

    // ── FriendRequest → FriendRequestDto ─────────────────────────────────────

    [Fact]
    public void ToDto_FriendRequest_MapsAllFields()
    {
        var fr = new FriendRequest
        {
            Id = 99, SenderId = 1, ReceiverId = 2,
            Sender = new User { Id = 1, Username = "alice" },
            Receiver = new User { Id = 2, Username = "bob" },
            Status = FriendRequestStatus.Pending,
            CreatedAt = BaseTime
        };
        var dto = DtoMapper.ToDto(fr);

        Assert.Equal(99, dto.Id);
        Assert.Equal(1, dto.SenderId);
        Assert.Equal("alice", dto.SenderUsername);
        Assert.Equal(2, dto.ReceiverId);
        Assert.Equal("bob", dto.ReceiverUsername);
        Assert.Equal("pending", dto.Status);
        Assert.Equal(BaseTime, dto.CreatedAt);
    }

    [Fact]
    public void ToDto_FriendRequest_AcceptedStatus_MapsToLowercase()
    {
        var fr = new FriendRequest
        {
            Id = 1, SenderId = 1, ReceiverId = 2,
            Sender = new User { Id = 1, Username = "a" },
            Receiver = new User { Id = 2, Username = "b" },
            Status = FriendRequestStatus.Accepted,
            CreatedAt = BaseTime
        };
        var dto = DtoMapper.ToDto(fr);
        Assert.Equal("accepted", dto.Status);
    }

    // ── Friend → FriendDto ────────────────────────────────────────────────────

    [Fact]
    public void ToFriendDto_MapsAllFields()
    {
        var user = new User { Id = 5, Username = "charlie", TotalXp = 300, Level = 2, Streak = 7, LastSubmissionAt = BaseTime };
        var dto = DtoMapper.ToFriendDto(user, DefaultCalc());

        Assert.Equal(5, dto.UserId);
        Assert.Equal("charlie", dto.Username);
        Assert.Equal(300, dto.TotalXp);
        Assert.Equal(7, dto.Streak);
        Assert.Equal(BaseTime, dto.LastSubmissionAt);
    }
}
