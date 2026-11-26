using DailyChallenges.Models;
using DailyChallenges.Data;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Ranking;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public sealed class EfLeagueStandingsRepository(AppDbContext db) : ILeagueStandingsRepository
{

    public async Task<List<LeagueLeaderboardRow>> GetLeaderboardAsync(LeagueGameDay gameDay, IRankingStrategy strategy, CancellationToken ct)
    {
        var memberIds = await GetMemberIdsAsync(gameDay.LeagueId, ct);
        if (memberIds.Count == 0) return [];

        var bestPerMember = await GetBestSubmissionPerMemberAsync(memberIds, gameDay, strategy, ct);
        var ranks = CompetitionRanking.RanksOf(bestPerMember.Select(s => s.ScoreValue).ToList());
        return bestPerMember
            .Select((s, index) => new LeagueLeaderboardRow(
                s.UserId, s.Username, ranks[index], s.Score, s.ScoreValue, s.SubmissionId, s.HasScreenshot))
            .ToList();
    }

    /// <inheritdoc />
    /// <remarks>Only the needed columns are projected, which keeps screenshot blobs out of the query.</remarks>
    public async Task<List<LeagueSubmissionRow>> GetMemberSubmissionsAsync(int leagueId, DateTime firstScoringDay, DateTime lastScoringDay, CancellationToken ct)
    {
        var memberIds = await GetMemberIdsAsync(leagueId, ct);
        if (memberIds.Count == 0) return [];

        return await db.Submissions
            .Where(s => s.UserId.HasValue && memberIds.Contains(s.UserId!.Value)
                        && s.ScoringDay >= firstScoringDay && s.ScoringDay <= lastScoringDay)
            .Select(s => new LeagueSubmissionRow(
                s.GameId,
                s.Game!.Name,
                s.Game.ScreenshotContentType != null && s.Game.ScreenshotContentType != "",
                s.Game!.RankingMode,
                s.Game!.ResetTime,
                s.UserId!.Value,
                s.Score,
                s.ScoreValue,
                s.ScoringDay,
                s.CreatedAt))
            .ToListAsync(ct);
    }

    private async Task<List<int>> GetMemberIdsAsync(int leagueId, CancellationToken ct) =>
        await db.LeagueMembers
            .Where(m => m.LeagueId == leagueId)
            .Select(m => m.UserId)
            .ToListAsync(ct);

    /// <summary>
    /// The rows come back in strategy order (best first), and GroupBy keeps the order in which keys first
    /// appear, so the first row of each member's group is their best and the groups stay in ranking order.
    /// </summary>
    private async Task<List<MemberBestSubmission>> GetBestSubmissionPerMemberAsync(
        List<int> memberIds, LeagueGameDay gameDay, IRankingStrategy strategy, CancellationToken ct)
    {
        var targetDay = gameDay.ScoringDay.Date;
        var scoredSubmissions = db.Submissions
            .Where(s =>
                s.GameId == gameDay.GameId &&
                s.ScoringDay == targetDay &&
                s.UserId.HasValue &&
                memberIds.Contains(s.UserId!.Value) &&
                s.ScoreValue.HasValue);

        var submissionsBestFirst = await strategy.ApplyOrdering(scoredSubmissions)
            .Select(s => new
            {
                s.Id,
                s.UserId,
                s.Score,
                s.ScoreValue,
                Username = s.Username != null ? s.Username : s.User != null ? s.User.Username : DisplayNames.Unknown,
                HasScreenshot = s.ScreenshotContentType != null && s.ScreenshotContentType != ""
            })
            .ToListAsync(ct);

        return submissionsBestFirst
            .GroupBy(s => s.UserId!.Value)
            .Select(memberSubmissions => memberSubmissions.First())
            .Select(s => new MemberBestSubmission(
                s.UserId!.Value, s.Username ?? DisplayNames.Unknown, s.Score, s.ScoreValue!.Value, s.Id, s.HasScreenshot))
            .ToList();
    }

    private sealed record MemberBestSubmission(
        int UserId, string Username, string Score, double ScoreValue, int SubmissionId, bool HasScreenshot);
}
