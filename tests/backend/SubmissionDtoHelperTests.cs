using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Services;
using Moq;

namespace DailyChallenges.Tests;

public class SubmissionDtoHelperTests
{
    private static readonly DateTime Day = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Submission WithScreenshot() => new()
    {
        Id = 5, GameId = 1, UserId = 2, Username = "bob", Score = "42", ScoreValue = 42,
        ScreenshotContentType = "image/png", ScoringDay = Day, CreatedAt = Day
    };

    [Fact]
    public void ToDtoWithScoringDay_UsesTheGivenDay()
    {
        var dto = SubmissionDtoHelper.ToDtoWithScoringDay(WithScreenshot(), Day.AddDays(3));

        Assert.Equal("2026-04-04", dto.ScoringDay);
        Assert.False(dto.IsDayWinner);
    }

    [Fact]
    public void ToDtoWithScoringDay_FallsBackToTheStoredDay()
    {
        var dto = SubmissionDtoHelper.ToDtoWithScoringDay(WithScreenshot());

        Assert.Equal("2026-04-01", dto.ScoringDay);
    }

    [Fact]
    public void ToDtoWithScoringDay_UnsetStoredDayLeavesTheDayEmpty()
    {
        var submission = WithScreenshot();
        submission.ScoringDay = DateTime.MinValue;

        Assert.Null(SubmissionDtoHelper.ToDtoWithScoringDay(submission).ScoringDay);
    }

    [Fact]
    public void ToDtoWithScoringDay_CarriesTheWinnerFlag()
    {
        Assert.True(SubmissionDtoHelper.ToDtoWithScoringDay(WithScreenshot(), Day, isDayWinner: true).IsDayWinner);
    }

    [Fact]
    public void Mask_HidesEverythingThatRevealsTheScore()
    {
        var dto = SubmissionDtoHelper.ToDtoWithScoringDay(WithScreenshot(), Day, isDayWinner: true);
        dto.Rank = 1;

        var masked = SubmissionDtoHelper.Mask(dto);

        Assert.Null(masked.Score);
        Assert.Null(masked.ScoreValue);
        Assert.Null(masked.Rank);
        Assert.Null(masked.ScreenshotUrl);
        Assert.False(masked.IsDayWinner);
        Assert.Equal(5, masked.Id);
        Assert.Equal("bob", masked.Username);
        Assert.Equal("2026-04-01", masked.ScoringDay);
    }

    [Fact]
    public void SignScreenshot_SignsOnlyWhenAsked()
    {
        var signer = new Mock<IScreenshotUrlSigner>();
        signer.Setup(s => s.Url(5, true)).Returns("signed");
        signer.Setup(s => s.Url(5, false)).Returns("/api/submissions/5/screenshot");

        Assert.Equal("signed", SubmissionDtoHelper.SignScreenshot(DtoMapper.ToDto(WithScreenshot()), signer.Object, true).ScreenshotUrl);
        Assert.Equal("/api/submissions/5/screenshot", SubmissionDtoHelper.SignScreenshot(DtoMapper.ToDto(WithScreenshot()), signer.Object, false).ScreenshotUrl);
    }

    [Fact]
    public void SignScreenshot_WithoutScreenshotStaysEmpty()
    {
        var submission = WithScreenshot();
        submission.ScreenshotContentType = null;

        var dto = SubmissionDtoHelper.SignScreenshot(DtoMapper.ToDto(submission), Mock.Of<IScreenshotUrlSigner>(), true);

        Assert.Null(dto.ScreenshotUrl);
    }

    [Theory]
    [InlineData(1, 10, 25, 3, true)]
    [InlineData(3, 10, 25, 3, false)]
    [InlineData(1, 10, 0, 0, false)]
    public void ToPageDto_ComputesPages(int page, int pageSize, int totalCount, int expectedPages, bool expectedHasMore)
    {
        var dto = SubmissionDtoHelper.ToPageDto(new List<SubmissionDto>(), page, pageSize, true, totalCount);

        Assert.Equal(expectedPages, dto.TotalPages);
        Assert.Equal(expectedHasMore, dto.HasMore);
        Assert.Equal(page, dto.Page);
        Assert.Equal(pageSize, dto.PageSize);
        Assert.Equal(totalCount, dto.TotalCount);
        Assert.True(dto.HasSubmittedForLatest);
        Assert.False(dto.ScoresHidden);
    }

    [Fact]
    public void ToPageDto_CarriesScoresHidden()
    {
        Assert.True(SubmissionDtoHelper.ToPageDto(new List<SubmissionDto>(), 1, 10, false, 0, scoresHidden: true).ScoresHidden);
    }

    [Fact]
    public void ToTodaySubmittersDto_CountsTheNames()
    {
        var dto = SubmissionDtoHelper.ToTodaySubmittersDto(new List<string> { "a", "b" });

        Assert.Equal(2, dto.Count);
        Assert.Equal(new[] { "a", "b" }, dto.Usernames);
    }
}
