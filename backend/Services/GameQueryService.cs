using System.Security.Claims;
using DailyChallenges.DTOs;
using DailyChallenges.Exceptions;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;

namespace DailyChallenges.Services;

public sealed class GameQueryService(
    IGameRepository games,
    ISubmissionRepository submissions,
    ISubmissionQueryService submissionQueries,
    IFavoriteRepository favorites,
    TimeProvider time,
    ILogger<GameQueryService> logger) : IGameQueryService
{
    private const string AvailableDatesPart = "availableDates";
    private const string HasSubmittedPart = "hasSubmitted";

    public async Task<List<GameDto>> GetAllAsync(ClaimsPrincipal? user)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var allGames = await games.GetAllAsync();
        var dtos = allGames.Select(g => DtoMapper.ToDto(g, nowUtc: now)).ToList();

        var userId = user.GetUserId();
        if (!userId.HasValue) return dtos;

        var currentDayByGame = allGames.ToDictionary(g => g.Id, g => ScoringDayCalculator.GetCurrentScoringDay(g.ResetTime, now).Date);
        await MarkUserFlagsAsync(dtos, currentDayByGame, userId.Value);
        return dtos;
    }

    public async Task<(byte[]? Data, string? ContentType)> GetImageAsync(int id)
    {
        var (data, contentType) = await games.GetBlobAsync(id);
        if (data == null || data.Length == 0) return (null, null);
        return (data, contentType);
    }

    /// <summary>
    /// Sub-requests run one after another (they share the scoped DbContext, which allows only one operation
    /// at a time), and failures are reported per part.
    /// </summary>
    public async Task<GameOverviewDto> GetOverviewAsync(int gameId, ClaimsPrincipal? user, string? include)
    {
        var game = await games.GetByIdWithoutBlobAsync(gameId) ?? throw new NotFoundException("Game not found");
        var dto = new GameOverviewDto { Game = DtoMapper.ToDto(game, nowUtc: time.GetUtcNow().UtcDateTime) };

        var parts = OverviewInclude.Parse(include);
        var loader = new OverviewPartLoader(logger);
        dto.AvailableDates = parts.Wants(AvailableDatesPart)
            ? await loader.LoadAsync(AvailableDatesPart, () => submissionQueries.GetAvailableDatesAsync(gameId), new List<string>())
            : new List<string>();
        dto.HasSubmittedForLatest = parts.Wants(HasSubmittedPart)
            && await loader.LoadAsync(HasSubmittedPart, () => submissionQueries.HasUserSubmittedForLatestAsync(gameId, user), false);
        dto.Errors = loader.Errors.Count > 0 ? loader.Errors : null;

        await MarkFavoriteAsync(dto.Game, user);
        return dto;
    }

    private async Task MarkUserFlagsAsync(List<GameDto> dtos, IReadOnlyDictionary<int, DateTime> currentDayByGame, int userId)
    {
        var userSubmissions = await submissions.GetByUserAndGamesAndDaysAsync(
            userId, currentDayByGame.Keys.ToList(), currentDayByGame.Values.Distinct().ToList());
        var submittedGameDays = userSubmissions.Select(s => (s.GameId, s.ScoringDay.Date)).ToHashSet();
        var favoriteIds = (await favorites.GetFavoriteGameIdsForUserAsync(userId)).ToHashSet();

        foreach (var dto in dtos)
        {
            dto.HasSubmittedForLatest = submittedGameDays.Contains((dto.Id, currentDayByGame[dto.Id]));
            dto.IsFavorite = favoriteIds.Contains(dto.Id);
        }
    }

    private async Task MarkFavoriteAsync(GameDto game, ClaimsPrincipal? user)
    {
        var userId = user.GetUserId();
        if (!userId.HasValue) return;

        var favoriteIds = await favorites.GetFavoriteGameIdsForUserAsync(userId.Value);
        game.IsFavorite = favoriteIds.Contains(game.Id);
    }

    /// <summary>The overview's comma-separated include filter; null or empty means every part.</summary>
    private sealed record OverviewInclude(HashSet<string>? Parts)
    {
        public static OverviewInclude Parse(string? include) =>
            new(string.IsNullOrEmpty(include)
                ? null
                : include.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(part => part.Trim()).ToHashSet());

        public bool Wants(string part) => Parts == null || Parts.Contains(part);
    }

    /// <summary>
    /// The failure boundary for one overview part: any exception is logged and reported in
    /// <see cref="Errors"/> with its message, and the part falls back to its empty value so the rest of the
    /// overview still renders.
    /// </summary>
    private sealed class OverviewPartLoader(ILogger logger)
    {
        public List<OverviewErrorDto> Errors { get; } = new();

        public async Task<T> LoadAsync<T>(string part, Func<Task<T>> load, T fallback)
        {
            try
            {
                return await load();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Game overview part {Part} failed", part);
                Errors.Add(new OverviewErrorDto { Part = part, Message = ex.Message });
                return fallback;
            }
        }
    }
}
