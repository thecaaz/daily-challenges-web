using System.Security.Claims;
using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services.Ranking;

namespace DailyChallenges.Services;

public sealed class GameHighscoreService(
    IGameRepository games,
    ISubmissionRepository submissions,
    IScoreVisibilityService visibility,
    IScreenshotUrlSigner signer) : IGameHighscoreService
{
    private const int TopLimit = 50;

    /// <summary>
    /// Hidden-scores rule: the current scoring day is left out (in SQL, on the stored ScoringDay) unless the
    /// caller may see it. Ordering and the top-50 limit also run in SQL.
    /// </summary>
    public async Task<HighscoreResult> GetHighscoreAsync(int id, ClaimsPrincipal? user)
    {
        var game = await GetExistingGameAsync(id);
        var strategy = RankingStrategyFactory.GetStrategy(game.RankingMode);
        var currentDay = visibility.GetCurrentScoringDay(game);
        var canSeeCurrent = await visibility.CanSeeDayAsync(user, game, currentDay);

        var top = await submissions.GetTopByScoreValueAsync(
            new TopScoresQuery(id, TopLimit) { ExcludeScoringDay = canSeeCurrent ? null : currentDay }, strategy);
        return ToRankedResult(top, currentDay);
    }

    /// <summary>The caller's own submissions are always visible.</summary>
    public async Task<HighscoreResult> GetPersonalHighscoreAsync(int id, int userId)
    {
        var game = await GetExistingGameAsync(id);
        var strategy = RankingStrategyFactory.GetStrategy(game.RankingMode);
        var currentDay = visibility.GetCurrentScoringDay(game);

        var top = await submissions.GetTopByScoreValueAsync(new TopScoresQuery(id, TopLimit) { UserId = userId }, strategy);
        return ToRankedResult(top, currentDay);
    }

    private async Task<Game> GetExistingGameAsync(int id) =>
        await games.GetByIdWithoutBlobAsync(id) ?? throw new KeyNotFoundException("Game not found");

    /// <summary>Screenshots of the current scoring day get signed URLs; scored entries are ranked 1, 2, 3… in order.</summary>
    private HighscoreResult ToRankedResult(List<Submission> top, DateTime currentDay)
    {
        var dtos = top
            .Select(s => SubmissionDtoMapper.WithScreenshotUrl(
                SubmissionDtoMapper.ToDtoWithScoringDay(s, s.ScoringDay.Date),
                s.ScoringDay.Date == currentDay ? signer.SignedUrl : signer.UnsignedUrl))
            .ToList();

        AssignSequentialRanks(dtos);
        return new HighscoreResult(dtos.FirstOrDefault(), dtos);
    }

    private static void AssignSequentialRanks(List<SubmissionDto> dtos)
    {
        var rank = 1;
        foreach (var dto in dtos)
            dto.Rank = dto.ScoreValue.HasValue ? rank++ : null;
    }
}
