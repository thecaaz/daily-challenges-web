using DailyChallenges.DTOs;
using DailyChallenges.Models;
using DailyChallenges.Services;

namespace DailyChallenges.Mapping;

public static class SubmissionDtoHelper
{
    /// <summary>
    /// Maps a submission with <paramref name="scoringDay"/>, or else its stored scoring day
    /// (left empty when the stored day was never set).
    /// </summary>
    public static SubmissionDto ToDtoWithScoringDay(Submission s, DateTime? scoringDay = null) =>
        DtoMapper.ToDto(s) with { ScoringDay = FormatDayOrNull(scoringDay ?? StoredDayOrNull(s)) };

    /// <summary>
    /// Hides everything that reveals the score: Score, ScoreValue, Rank, ScreenshotUrl and the winner flag.
    /// Id, GameId, UserId, Username, CreatedAt and ScoringDay stay.
    /// </summary>
    public static SubmissionDto Mask(SubmissionDto dto) =>
        dto with { Score = null, ScoreValue = null, Rank = null, ScreenshotUrl = null, IsDayWinner = false };

    /// <summary>
    /// Replaces the plain screenshot URL with <paramref name="screenshotUrl"/>, e.g. a signed one for
    /// current-day submissions the caller may see. Does nothing when there is no screenshot.
    /// </summary>
    public static SubmissionDto WithScreenshotUrl(SubmissionDto dto, Func<int, string> screenshotUrl) =>
        dto.ScreenshotUrl == null ? dto : dto with { ScreenshotUrl = screenshotUrl(dto.Id) };

    public static TodaySubmittersDto ToTodaySubmittersDto(List<string> usernames) =>
        new() { Count = usernames.Count, Usernames = usernames };

    private static DateTime? StoredDayOrNull(Submission s) =>
        s.ScoringDay != DateTime.MinValue ? s.ScoringDay.Date : null;

    private static string? FormatDayOrNull(DateTime? day) =>
        day.HasValue ? ScoringDayHelper.FormatScoringDay(day.Value) : null;
}
