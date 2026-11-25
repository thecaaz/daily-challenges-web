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
    public void Mask_HidesEverythingThatRevealsTheScore()
    {
        var dto = SubmissionDtoHelper.ToDtoWithScoringDay(WithScreenshot(), Day) with { IsDayWinner = true, Rank = 1 };

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
    public void WithScreenshotUrl_UsesTheGivenUrl()
    {
        var signer = new Mock<IScreenshotUrlSigner>();
        signer.Setup(s => s.SignedUrl(5)).Returns("signed");
        signer.Setup(s => s.UnsignedUrl(5)).Returns("/api/submissions/5/screenshot");

        Assert.Equal("signed", SubmissionDtoHelper.WithScreenshotUrl(DtoMapper.ToDto(WithScreenshot()), signer.Object.SignedUrl).ScreenshotUrl);
        Assert.Equal("/api/submissions/5/screenshot", SubmissionDtoHelper.WithScreenshotUrl(DtoMapper.ToDto(WithScreenshot()), signer.Object.UnsignedUrl).ScreenshotUrl);
    }

    [Fact]
    public void WithScreenshotUrl_WithoutScreenshotStaysEmpty()
    {
        var submission = WithScreenshot();
        submission.ScreenshotContentType = null;

        var dto = SubmissionDtoHelper.WithScreenshotUrl(DtoMapper.ToDto(submission), Mock.Of<IScreenshotUrlSigner>().SignedUrl);

        Assert.Null(dto.ScreenshotUrl);
    }

    [Theory]
    [InlineData(1, 10, 25, 3, true)]
    [InlineData(3, 10, 25, 3, false)]
    [InlineData(1, 10, 0, 0, false)]
    public void PageDto_ComputesPages(int page, int pageSize, int totalCount, int expectedPages, bool expectedHasMore)
    {
        var dto = new SubmissionPageDto { Page = page, PageSize = pageSize, TotalCount = totalCount, HasSubmittedForLatest = true };

        Assert.Equal(expectedPages, dto.TotalPages);
        Assert.Equal(expectedHasMore, dto.HasMore);
        Assert.Equal(page, dto.Page);
        Assert.Equal(pageSize, dto.PageSize);
        Assert.Equal(totalCount, dto.TotalCount);
        Assert.True(dto.HasSubmittedForLatest);
        Assert.False(dto.ScoresHidden);
    }

    [Fact]
    public void PageDto_SerializesThePagingFieldsInOrder()
    {
        var dto = new SubmissionPageDto { Page = 1, PageSize = 10, TotalCount = 11, ScoresHidden = true };

        var json = System.Text.Json.JsonSerializer.Serialize(dto, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Equal("""{"items":[],"hasSubmittedForLatest":false,"scoresHidden":true,"hasMore":true,"page":1,"pageSize":10,"totalCount":11,"totalPages":2}""", json);
    }

    [Fact]
    public void ToTodaySubmittersDto_CountsTheNames()
    {
        var dto = SubmissionDtoHelper.ToTodaySubmittersDto(new List<string> { "a", "b" });

        Assert.Equal(2, dto.Count);
        Assert.Equal(new[] { "a", "b" }, dto.Usernames);
    }
}
