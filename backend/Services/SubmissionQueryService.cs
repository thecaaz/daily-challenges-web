using DailyChallenges.DTOs;
using DailyChallenges.Exceptions;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services.Ranking;
using System.Security.Claims;

namespace DailyChallenges.Services;

public sealed class SubmissionQueryService(
    ISubmissionRepository submissions,
    IGameRepository games,
    IUserSubmissionChecker submissionChecker,
    IScoreVisibilityService visibility,
    IScreenshotUrlSigner screenshotSigner,
    TimeProvider time) : ISubmissionQueryService
{
    private const int FirstPlace = 1;

    public async Task<SubmissionPageDto> GetByGameAsync(int gameId, ClaimsPrincipal? user, DateTime? scoringDay = null, int page = 1, int pageSize = 50)
    {
        var game = await games.GetByIdWithoutBlobAsync(gameId);
        if (game == null) return SubmissionDtoHelper.ToPageDto([], page, pageSize, false, 0);

        var currentDay = visibility.GetCurrentScoringDay(game);
        var hasSubmittedForLatest = await submissionChecker.HasUserSubmittedForDayAsync(user.GetUserId(), gameId, currentDay);
        var excludedDay = scoringDay.HasValue || hasSubmittedForLatest ? (DateTime?)null : currentDay;
        var areScoresHidden = scoringDay?.Date == currentDay && !await visibility.CanSeeDayAsync(user, game, currentDay);

        var (pageItems, totalCount) = await submissions.GetByGameFilteredAsync(gameId, page, pageSize, null, scoringDay, excludedDay);
        if (areScoresHidden)
            return SubmissionDtoHelper.ToPageDto(ToMaskedDtos(pageItems), page, pageSize, hasSubmittedForLatest, totalCount, scoresHidden: true);

        var strategy = RankingStrategyFactory.GetStrategy(game.RankingMode);
        var dtos = await ToDtosWithWinnerFlagsAsync(gameId, pageItems, strategy, currentDay);
        if (scoringDay.HasValue)
            await AssignRanksForDayAsync(gameId, scoringDay.Value.Date, strategy, dtos);

        return SubmissionDtoHelper.ToPageDto(dtos, page, pageSize, hasSubmittedForLatest, totalCount);
    }

    private static List<SubmissionDto> ToMaskedDtos(List<Submission> pageItems) =>
        pageItems
            .Select(s => SubmissionDtoHelper.Mask(SubmissionDtoHelper.ToDtoWithScoringDay(s, s.ScoringDay.Date)))
            .ToList();

    private async Task<List<SubmissionDto>> ToDtosWithWinnerFlagsAsync(int gameId, List<Submission> pageItems, IRankingStrategy strategy, DateTime currentDay)
    {
        var winnersByDay = await GetWinnersByDayAsync(gameId, pageItems, strategy);
        return pageItems.Select(s => ToDtoWithWinnerFlag(s, winnersByDay, currentDay)).ToList();
    }

    /// <summary>Winners only of the scoring days present in the page.</summary>
    private async Task<Dictionary<DateTime, Submission>> GetWinnersByDayAsync(int gameId, List<Submission> pageItems, IRankingStrategy strategy)
    {
        var daysInPage = pageItems.Select(s => s.ScoringDay.Date).Distinct().ToList();
        if (daysInPage.Count == 0) return [];

        var winners = await submissions.GetWinnersForGameAndDaysAsync(gameId, daysInPage, strategy);
        return winners.ToDictionary(w => w.ScoringDay.Date);
    }

    private SubmissionDto ToDtoWithWinnerFlag(Submission submission, Dictionary<DateTime, Submission> winnersByDay, DateTime currentDay)
    {
        var day = submission.ScoringDay.Date;
        var isWinner = winnersByDay.TryGetValue(day, out var winner) && winner.Id == submission.Id;
        var dto = SubmissionDtoHelper.ToDtoWithScoringDay(submission, day, isWinner);
        return SignScreenshotOnCurrentDay(dto, day, currentDay);
    }

    /// <summary>1-based rank among the day's scored submissions; null for unscored rows.</summary>
    private async Task AssignRanksForDayAsync(int gameId, DateTime scoringDay, IRankingStrategy strategy, List<SubmissionDto> dtos)
    {
        var rankedForDay = await submissions.GetByGameAndDayByScoreValueAsync(gameId, scoringDay, strategy);
        var rankById = rankedForDay
            .Select((submission, index) => (submission.Id, Rank: index + 1))
            .ToDictionary(entry => entry.Id, entry => entry.Rank);

        foreach (var dto in dtos)
            dto.Rank = rankById.TryGetValue(dto.Id, out var rank) ? rank : null;
    }

    public Task<bool> HasUserSubmittedForLatestAsync(int gameId, ClaimsPrincipal? user) =>
        submissionChecker.HasUserSubmittedForLatestAsync(gameId, user);

    public async Task<TodaySubmittersDto> GetTodaySubmittersAsync(int gameId)
    {
        var game = await games.GetByIdWithoutBlobAsync(gameId);
        var usernames = await submissions.GetUsernamesForDayAsync(gameId, GetCurrentScoringDay(game));
        return SubmissionDtoHelper.ToTodaySubmittersDto(usernames);
    }

    public async Task<List<SubmissionDto>> GetUnfilteredByGameAsync(int gameId)
    {
        var game = await games.GetByIdWithoutBlobAsync(gameId);
        var gameSubmissions = await submissions.GetByGameAsync(gameId);
        var currentDay = GetCurrentScoringDay(game);
        return gameSubmissions.Select(s => ToAdminDto(s, currentDay)).ToList();
    }

    /// <summary>
    /// Reports the stored scoring day, as everywhere else (a later reset-time change must not move old
    /// submissions). Admins may see the current day, so its screenshots get signed URLs.
    /// </summary>
    private SubmissionDto ToAdminDto(Submission submission, DateTime currentDay)
    {
        var dto = DtoMapper.ToDto(submission);
        dto.ScoringDay = ScoringDayCalculator.FormatScoringDay(submission.ScoringDay);
        return SignScreenshotOnCurrentDay(dto, submission.ScoringDay.Date, currentDay);
    }

    /// <summary>A missing game falls back to a midnight UTC reset.</summary>
    private DateTime GetCurrentScoringDay(Game? game) =>
        ScoringDayCalculator.GetCurrentScoringDay(game?.ResetTime ?? TimeSpan.Zero, time.GetUtcNow().UtcDateTime).Date;

    public async Task<List<string>> GetAvailableDatesAsync(int gameId)
    {
        var dates = await submissions.GetAvailableDatesAsync(gameId);
        return dates
            .Select(ScoringDayCalculator.FormatScoringDay)
            .Distinct()
            .OrderByDescending(day => day)
            .ToList();
    }

    public async Task<SubmissionDto?> GetWinnerAsync(int gameId, DateTime? scoringDay, ClaimsPrincipal? user)
    {
        var game = await games.GetByIdWithoutBlobAsync(gameId);
        if (game == null) return null;

        var currentDay = visibility.GetCurrentScoringDay(game);
        var day = scoringDay?.Date ?? GetPreviousDay(currentDay);
        if (!await visibility.CanSeeDayAsync(user, game, day)) return null;

        var strategy = RankingStrategyFactory.GetStrategy(game.RankingMode);
        var winner = await submissions.GetWinnerForGameAndDayAsync(gameId, day, strategy);
        if (winner == null) return null;

        var winnerDay = winner.ScoringDay.Date;
        var dto = SubmissionDtoHelper.ToDtoWithScoringDay(winner, winnerDay, isDayWinner: true);
        dto.Rank = FirstPlace;
        return SignScreenshotOnCurrentDay(dto, winnerDay, currentDay);
    }

    private static DateTime GetPreviousDay(DateTime day) => day.AddDays(-1);

    public async Task<SubmissionDto?> GetByIdAsync(int id, ClaimsPrincipal? user)
    {
        var submission = await submissions.GetByIdWithoutBlobAsync(id);
        if (submission == null) return null;

        var day = submission.ScoringDay.Date;
        var dto = SubmissionDtoHelper.ToDtoWithScoringDay(submission, day);

        var game = await games.GetByIdWithoutBlobAsync(submission.GameId);
        if (game == null) return SubmissionDtoHelper.Mask(dto);
        if (!await CanSeeSubmissionAsync(user, game, submission)) return SubmissionDtoHelper.Mask(dto);

        await AssignRankAndWinnerFlagAsync(dto, game, submission);
        return SubmissionDtoHelper.SignScreenshot(dto, screenshotSigner, visibility.IsCurrentDay(game, day));
    }

    /// <summary>The owner always sees their own submission; anyone else follows the hidden-scores rule.</summary>
    private async Task<bool> CanSeeSubmissionAsync(ClaimsPrincipal? user, Game game, Submission submission)
    {
        var userId = user.GetUserId();
        var isOwner = userId.HasValue && submission.UserId == userId;
        return isOwner || await visibility.CanSeeDayAsync(user, game, submission.ScoringDay.Date);
    }

    private async Task AssignRankAndWinnerFlagAsync(SubmissionDto dto, Game game, Submission submission)
    {
        if (!submission.ScoreValue.HasValue) return;

        var strategy = RankingStrategyFactory.GetStrategy(game.RankingMode);
        var rankedForDay = await submissions.GetByGameAndDayByScoreValueAsync(game.Id, submission.ScoringDay.Date, strategy);
        var index = rankedForDay.FindIndex(s => s.Id == submission.Id);
        if (index < 0) return;

        dto.Rank = index + 1;
        dto.IsDayWinner = dto.Rank == FirstPlace;
    }

    /// <summary>
    /// &lt;img&gt; requests carry no Bearer token, so a current-day screenshot is served only with a valid
    /// signature (handed out with the DTO to callers allowed to see it). Past days are public.
    /// </summary>
    public async Task<(byte[]? Data, string? ContentType)> GetScreenshotAsync(int id, long? exp, string? sig)
    {
        var submission = await submissions.GetByIdWithoutBlobAsync(id);
        if (submission == null) return (null, null);

        var game = await games.GetByIdWithoutBlobAsync(submission.GameId);
        var isSignatureRequired = game != null && visibility.IsCurrentDay(game, submission.ScoringDay);
        if (isSignatureRequired && !screenshotSigner.IsValid(id, exp, sig))
            throw new ForbiddenException("Screenshot is hidden until you submit for this day.");

        var (data, contentType) = await submissions.GetBlobAsync(id);
        return data is { Length: > 0 } ? (data, contentType) : (null, null);
    }

    /// <summary>Current-day screenshots need a signed URL (the screenshot endpoint checks it); past days do not.</summary>
    private SubmissionDto SignScreenshotOnCurrentDay(SubmissionDto dto, DateTime day, DateTime currentDay) =>
        SubmissionDtoHelper.SignScreenshot(dto, screenshotSigner, day == currentDay);
}
