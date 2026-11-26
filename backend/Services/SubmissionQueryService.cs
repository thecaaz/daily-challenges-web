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
    IScreenshotUrlSigner screenshotSigner) : ISubmissionQueryService
{
    private const int FirstPlace = 1;

    /// <summary>The game, ranking and current scoring day a page of submissions is built for.</summary>
    private sealed record GameDayContext(int GameId, IRankingStrategy Strategy, DateTime CurrentDay);

    public async Task<SubmissionPageDto> GetByGameAsync(GameSubmissionsPageRequest request, ClaimsPrincipal? user, CancellationToken ct)
    {
        var game = await games.GetByIdWithoutBlobAsync(request.GameId, ct);
        if (game == null) return new SubmissionPageDto { Page = request.Page, PageSize = request.PageSize };

        var context = new GameDayContext(request.GameId, RankingStrategyFactory.GetStrategy(game.RankingMode), visibility.GetCurrentScoringDay(game));
        var hasSubmittedForLatest = await submissionChecker.HasUserSubmittedForDayAsync(user.GetUserId(), context.GameId, context.CurrentDay, ct);
        var areScoresHidden = request.ScoringDay?.Date == context.CurrentDay && !await visibility.CanSeeDayAsync(user, game, context.CurrentDay, ct);
        var (pageItems, totalCount) = await GetPageItemsAsync(request, context.CurrentDay, hasSubmittedForLatest, ct);
        var items = areScoresHidden ? ToMaskedDtos(pageItems) : await ToRankedDtosAsync(context, pageItems, request.ScoringDay, ct);

        return new SubmissionPageDto
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            HasSubmittedForLatest = hasSubmittedForLatest,
            ScoresHidden = areScoresHidden
        };
    }

    /// <summary>Without a requested day, the current day stays out of the list until the caller has submitted for it.</summary>
    private Task<(List<Submission> Items, int TotalCount)> GetPageItemsAsync(GameSubmissionsPageRequest request, DateTime currentDay, bool hasSubmittedForLatest, CancellationToken ct)
    {
        var excludedDay = request.ScoringDay.HasValue || hasSubmittedForLatest ? (DateTime?)null : currentDay;
        var query = new SubmissionPageQuery(request.GameId, request.Page, request.PageSize) { ScoringDay = request.ScoringDay, ExcludeScoringDay = excludedDay };
        return submissions.GetByGameFilteredAsync(query, ct);
    }

    private static List<SubmissionDto> ToMaskedDtos(List<Submission> pageItems) =>
        pageItems
            .Select(s => SubmissionDtoMapper.Mask(SubmissionDtoMapper.ToDtoWithScoringDay(s, s.ScoringDay.Date)))
            .ToList();

    /// <summary>Winner flags for every row; ranks only when a single scoring day was requested.</summary>
    private async Task<List<SubmissionDto>> ToRankedDtosAsync(GameDayContext context, List<Submission> pageItems, DateTime? scoringDay, CancellationToken ct)
    {
        var dtos = await ToDtosWithWinnerFlagsAsync(context, pageItems, ct);
        if (scoringDay.HasValue)
            await AssignRanksForDayAsync(context, scoringDay.Value.Date, dtos, ct);
        return dtos;
    }

    private async Task<List<SubmissionDto>> ToDtosWithWinnerFlagsAsync(GameDayContext context, List<Submission> pageItems, CancellationToken ct)
    {
        var winnersByDay = await GetWinnersByDayAsync(context, pageItems, ct);
        return pageItems.Select(s => ToDtoWithWinnerFlag(s, winnersByDay, context.CurrentDay)).ToList();
    }

    /// <summary>Winners only of the scoring days present in the page.</summary>
    private async Task<Dictionary<DateTime, Submission>> GetWinnersByDayAsync(GameDayContext context, List<Submission> pageItems, CancellationToken ct)
    {
        var daysInPage = pageItems.Select(s => s.ScoringDay.Date).Distinct().ToList();
        if (daysInPage.Count == 0) return [];

        var winners = await submissions.GetWinnersForGameAndDaysAsync(context.GameId, daysInPage, context.Strategy, ct);
        return winners.ToDictionary(w => w.ScoringDay.Date);
    }

    private SubmissionDto ToDtoWithWinnerFlag(Submission submission, Dictionary<DateTime, Submission> winnersByDay, DateTime currentDay)
    {
        var day = submission.ScoringDay.Date;
        var isWinner = winnersByDay.TryGetValue(day, out var winner) && winner.Id == submission.Id;
        var dto = SubmissionDtoMapper.ToDtoWithScoringDay(submission, day) with { IsDayWinner = isWinner };
        return SignScreenshotOnCurrentDay(dto, day, currentDay);
    }

    /// <summary>Null for unscored rows.</summary>
    private async Task AssignRanksForDayAsync(GameDayContext context, DateTime scoringDay, List<SubmissionDto> dtos, CancellationToken ct)
    {
        var rankById = await GetRanksForDayAsync(context.GameId, scoringDay, context.Strategy, ct);
        foreach (var dto in dtos)
            dto.Rank = rankById.TryGetValue(dto.Id, out var rank) ? rank : null;
    }

    /// <summary>1-based rank of each of the day's scored submissions, by submission id.</summary>
    private async Task<Dictionary<int, int>> GetRanksForDayAsync(int gameId, DateTime scoringDay, IRankingStrategy strategy, CancellationToken ct)
    {
        var rankedForDay = await submissions.GetByGameAndDayByScoreValueAsync(gameId, scoringDay, strategy, ct);
        return rankedForDay
            .Select((submission, index) => (submission.Id, Rank: index + 1))
            .ToDictionary(entry => entry.Id, entry => entry.Rank);
    }

    public Task<bool> HasUserSubmittedForLatestAsync(int gameId, ClaimsPrincipal? user, CancellationToken ct) =>
        submissionChecker.HasUserSubmittedForLatestAsync(gameId, user, ct);

    /// <summary>A missing game has no submissions (they are deleted with it), so nobody submitted.</summary>
    public async Task<TodaySubmittersDto> GetTodaySubmittersAsync(int gameId, CancellationToken ct)
    {
        var game = await games.GetByIdWithoutBlobAsync(gameId, ct);
        if (game == null) return SubmissionDtoMapper.ToTodaySubmittersDto([]);

        var usernames = await submissions.GetUsernamesForDayAsync(gameId, visibility.GetCurrentScoringDay(game), ct);
        return SubmissionDtoMapper.ToTodaySubmittersDto(usernames.Select(name => name ?? DisplayNames.Anonymous).ToList());
    }

    /// <summary>A missing game has no submissions (they are deleted with it).</summary>
    public async Task<List<SubmissionDto>> GetUnfilteredByGameAsync(int gameId, CancellationToken ct)
    {
        var game = await games.GetByIdWithoutBlobAsync(gameId, ct);
        if (game == null) return [];

        var gameSubmissions = await submissions.GetByGameAsync(gameId, ct);
        var currentDay = visibility.GetCurrentScoringDay(game);
        return gameSubmissions.Select(s => ToAdminDto(s, currentDay)).ToList();
    }

    /// <summary>
    /// Reports the stored scoring day, as everywhere else (a later reset-time change must not move old
    /// submissions). Admins may see the current day, so its screenshots get signed URLs.
    /// </summary>
    private SubmissionDto ToAdminDto(Submission submission, DateTime currentDay)
    {
        var dto = DtoMapper.ToDto(submission) with { ScoringDay = ScoringDayCalculator.FormatScoringDay(submission.ScoringDay) };
        return SignScreenshotOnCurrentDay(dto, submission.ScoringDay.Date, currentDay);
    }

    public async Task<List<string>> GetAvailableDatesAsync(int gameId, CancellationToken ct)
    {
        var dates = await submissions.GetAvailableDatesAsync(gameId, ct);
        return dates
            .Select(ScoringDayCalculator.FormatScoringDay)
            .Distinct()
            .OrderByDescending(day => day)
            .ToList();
    }

    public async Task<SubmissionDto?> GetWinnerAsync(int gameId, DateTime? scoringDay, ClaimsPrincipal? user, CancellationToken ct)
    {
        var game = await games.GetByIdWithoutBlobAsync(gameId, ct);
        if (game == null) return null;

        var currentDay = visibility.GetCurrentScoringDay(game);
        var day = scoringDay?.Date ?? GetPreviousDay(currentDay);
        if (!await visibility.CanSeeDayAsync(user, game, day, ct)) return null;

        var strategy = RankingStrategyFactory.GetStrategy(game.RankingMode);
        var winner = await submissions.GetWinnerForGameAndDayAsync(gameId, day, strategy, ct);
        if (winner == null) return null;

        var winnerDay = winner.ScoringDay.Date;
        var dto = SubmissionDtoMapper.ToDtoWithScoringDay(winner, winnerDay) with { IsDayWinner = true, Rank = FirstPlace };
        return SignScreenshotOnCurrentDay(dto, winnerDay, currentDay);
    }

    private static DateTime GetPreviousDay(DateTime day) => day.AddDays(-1);

    public async Task<SubmissionDto?> GetByIdAsync(int id, ClaimsPrincipal? user, CancellationToken ct)
    {
        var submission = await submissions.GetByIdWithoutBlobAsync(id, ct);
        if (submission == null) return null;

        var day = submission.ScoringDay.Date;
        var dto = SubmissionDtoMapper.ToDtoWithScoringDay(submission, day);

        var game = await games.GetByIdWithoutBlobAsync(submission.GameId, ct);
        if (game == null) return SubmissionDtoMapper.Mask(dto);
        if (!await CanSeeSubmissionAsync(user, game, submission, ct)) return SubmissionDtoMapper.Mask(dto);

        await AssignRankAndWinnerFlagAsync(dto, game, submission, ct);
        return SignScreenshotOnCurrentDay(dto, day, visibility.GetCurrentScoringDay(game));
    }

    /// <summary>The owner always sees their own submission; anyone else follows the hidden-scores rule.</summary>
    private async Task<bool> CanSeeSubmissionAsync(ClaimsPrincipal? user, Game game, Submission submission, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var isOwner = userId.HasValue && submission.UserId == userId;
        return isOwner || await visibility.CanSeeDayAsync(user, game, submission.ScoringDay.Date, ct);
    }

    private async Task AssignRankAndWinnerFlagAsync(SubmissionDto dto, Game game, Submission submission, CancellationToken ct)
    {
        if (!submission.ScoreValue.HasValue) return;

        var strategy = RankingStrategyFactory.GetStrategy(game.RankingMode);
        var rankById = await GetRanksForDayAsync(game.Id, submission.ScoringDay.Date, strategy, ct);
        if (!rankById.TryGetValue(submission.Id, out var rank)) return;

        dto.Rank = rank;
        dto.IsDayWinner = rank == FirstPlace;
    }

    /// <summary>
    /// &lt;img&gt; requests carry no Bearer token, so a current-day screenshot is served only with a valid
    /// signature (handed out with the DTO to callers allowed to see it). Past days are public.
    /// </summary>
    public async Task<(byte[]? Data, string? ContentType)> GetScreenshotAsync(int id, long? exp, string? sig, CancellationToken ct)
    {
        var submission = await submissions.GetByIdWithoutBlobAsync(id, ct);
        if (submission == null) return (null, null);

        var game = await games.GetByIdWithoutBlobAsync(submission.GameId, ct);
        var isSignatureRequired = game != null && visibility.IsCurrentDay(game, submission.ScoringDay);
        if (isSignatureRequired && !screenshotSigner.IsValid(id, exp, sig))
            throw new ForbiddenException("Screenshot is hidden until you submit for this day.");

        var (data, contentType) = await submissions.GetBlobAsync(id, ct);
        return data is { Length: > 0 } ? (data, contentType) : (null, null);
    }

    /// <summary>Current-day screenshots need a signed URL (the screenshot endpoint checks it); past days do not.</summary>
    private SubmissionDto SignScreenshotOnCurrentDay(SubmissionDto dto, DateTime day, DateTime currentDay) =>
        SubmissionDtoMapper.WithScreenshotUrl(dto, day == currentDay ? screenshotSigner.SignedUrl : screenshotSigner.UnsignedUrl);
}
