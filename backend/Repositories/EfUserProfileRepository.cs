using DailyChallenges.Data;
using DailyChallenges.DTOs;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public sealed class EfUserProfileRepository(AppDbContext db) : IUserProfileRepository
{
    private const int DefaultTopGames = 10;

    /// <summary>Scores are matched to their submission after rounding to this many decimals (float equality is unreliable).</summary>
    private const int ScoreMatchDecimals = 2;

    private const string HighestRankingMode = "highest";
    private const string LowestRankingMode = "lowest";

    public Task<User?> GetByIdAsync(int userId) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

    public Task<User?> GetByUsernameAsync(string username) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username);

    /// <summary>
    /// Most played games first. The best score is the max for <see cref="RankingMode.Highest"/> games and the
    /// min otherwise, so a third ranking mode needs this projection adjusted.
    /// </summary>
    public async Task<List<UserGameStatDto>> GetMostPlayedGamesAsync(int userId, int topGames = DefaultTopGames, IReadOnlyCollection<int>? excludeSubmissionIds = null)
    {
        var limit = topGames < 1 ? DefaultTopGames : topGames;
        var excluded = (excludeSubmissionIds ?? Array.Empty<int>()).ToList();

        var stats = await GameStatsQuery(userId, excluded)
            .OrderByDescending(x => x.Plays)
            .Take(limit)
            .ToListAsync();

        await PopulateBestSubmissionIdAsync(userId, stats, excluded);
        return stats;
    }

    private IQueryable<UserGameStatDto> GameStatsQuery(int userId, List<int> excluded)
    {
        var grouped = ScoredSubmissions(userId, excluded)
            .GroupBy(s => s.GameId)
            .Select(g => new
            {
                GameId = g.Key,
                Plays = g.Count(),
                MaxScore = g.Max(x => x.ScoreValue),
                MinScore = g.Min(x => x.ScoreValue),
                LastPlayed = g.Max(x => x.CreatedAt)
            });

        return from g in grouped
               join gm in db.Games.AsNoTracking() on g.GameId equals gm.Id
               select new UserGameStatDto
               {
                   GameId = g.GameId,
                   Name = gm.Name,
                   Url = gm.Url,
                   Plays = g.Plays,
                   HighestScore = gm.RankingMode == RankingMode.Highest ? g.MaxScore : g.MinScore,
                   LastPlayed = g.LastPlayed,
                   RankingMode = gm.RankingMode == RankingMode.Highest ? HighestRankingMode : LowestRankingMode
               };
    }

    /// <summary>The newest submission with each game's best score.</summary>
    private async Task PopulateBestSubmissionIdAsync(int userId, List<UserGameStatDto> stats, List<int> excluded)
    {
        var gameIdsWithScore = stats.Where(x => x.HighestScore.HasValue).Select(x => x.GameId).ToList();
        if (gameIdsWithScore.Count == 0) return;

        var scoreLookup = await ScoredSubmissions(userId, excluded)
            .Where(s => gameIdsWithScore.Contains(s.GameId))
            .GroupBy(s => new { s.GameId, ScoreKey = Math.Round(s.ScoreValue!.Value, ScoreMatchDecimals) })
            .Select(g => new
            {
                g.Key.GameId,
                g.Key.ScoreKey,
                SubmissionId = g.OrderByDescending(x => x.CreatedAt).Select(x => x.Id).FirstOrDefault()
            })
            .ToListAsync();

        var submissionIdByGameScore = scoreLookup.ToDictionary(x => (x.GameId, x.ScoreKey), x => (int?)x.SubmissionId);
        foreach (var stat in stats)
        {
            if (stat.HighestScore is not { } bestScore) continue;

            submissionIdByGameScore.TryGetValue((stat.GameId, Math.Round(bestScore, ScoreMatchDecimals)), out var submissionId);
            stat.BestSubmissionId = submissionId;
        }
    }

    private IQueryable<Submission> ScoredSubmissions(int userId, List<int> excluded) =>
        db.Submissions.Where(s => s.UserId == userId && s.ScoreValue != null && !excluded.Contains(s.Id));
}
