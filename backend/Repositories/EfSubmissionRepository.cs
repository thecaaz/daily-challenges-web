using DailyChallenges.Data;
using DailyChallenges.Exceptions;
using DailyChallenges.Models;
using DailyChallenges.Services.Ranking;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public sealed class EfSubmissionRepository(AppDbContext db) : ISubmissionRepository
{
    private const int DefaultTopCount = 10;
    private const string AnonymousUsername = "Anonymous";

    public Task<List<Submission>> GetByGameAsync(int gameId) =>
        db.Submissions.ForGame(gameId).NewestFirst().SelectWithoutBlob().ToListAsync();

    public Task<List<Submission>> GetTopByScoreValueAsync(TopScoresQuery query, IRankingStrategy strategy)
    {
        var scored = db.Submissions.ForGame(query.GameId).Scored().ExcludingScoringDay(query.ExcludeScoringDay);
        if (query.UserId.HasValue)
            scored = scored.Where(s => s.UserId == query.UserId.Value);

        return strategy
            .ApplyOrdering(scored)
            .Take(query.Top < 1 ? DefaultTopCount : query.Top)
            .SelectWithoutBlob()
            .ToListAsync();
    }

    public Task<List<Submission>> GetByGameAndDayByScoreValueAsync(int gameId, DateTime scoringDay, IRankingStrategy strategy) =>
        strategy
            .ApplyOrdering(db.Submissions.ForGame(gameId).OnScoringDay(scoringDay).Scored())
            .SelectWithoutBlob()
            .ToListAsync();

    public Task<List<DateTime>> GetAvailableDatesAsync(int gameId) =>
        db.Submissions
            .ForGame(gameId)
            .AsNoTracking()
            .Select(s => s.ScoringDay)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync();

    public async Task<(List<Submission> Items, int TotalCount)> GetByGameFilteredAsync(SubmissionPageQuery query)
    {
        var filtered = db.Submissions
            .ForGame(query.GameId)
            .MatchingSearch(query.Search)
            .OnScoringDayIfGiven(query.ScoringDay)
            .ExcludingScoringDay(query.ExcludeScoringDay);

        var total = await filtered.CountAsync();
        var items = await filtered.NewestFirst().TakePage(query.Page, query.PageSize).SelectWithoutBlob().ToListAsync();
        return (items, total);
    }

    public async Task<Submission?> GetWinnerForGameAndDayAsync(int gameId, DateTime scoringDay, IRankingStrategy strategy)
    {
        var scored = db.Submissions.ForGame(gameId).OnScoringDay(scoringDay).Scored();
        if (!await scored.AnyAsync()) return null;

        return await strategy.ApplyOrdering(scored).SelectWithoutBlob().FirstOrDefaultAsync();
    }

    /// <summary>Ranks each day in memory over the days' scored rows, which are loaded in one query.</summary>
    public async Task<List<Submission>> GetWinnersForGameAndDaysAsync(int gameId, List<DateTime> days, IRankingStrategy strategy)
    {
        if (days == null || days.Count == 0) return [];

        var submissions = await db.Submissions
            .Where(s => s.GameId == gameId && days.Contains(s.ScoringDay) && s.ScoreValue.HasValue)
            .SelectWithoutBlob()
            .ToListAsync();

        return submissions
            .GroupBy(s => s.ScoringDay)
            .Select(day => strategy.ApplyOrdering(day.AsQueryable()).FirstOrDefault())
            .OfType<Submission>()
            .ToList();
    }

    public async Task<List<Submission>> GetByUserAndGamesAndDaysAsync(int userId, List<int> gameIds, List<DateTime> scoringDays)
    {
        if (gameIds == null || gameIds.Count == 0 || scoringDays == null || scoringDays.Count == 0) return [];

        var targetDates = scoringDays.Select(d => d.Date).ToList();
        return await db.Submissions
            .Where(s => s.UserId == userId && gameIds.Contains(s.GameId) && targetDates.Contains(s.ScoringDay))
            .SelectWithoutBlob()
            .ToListAsync();
    }

    public Task<bool> ExistsForUserGameDayAsync(int userId, int gameId, DateTime scoringDay)
    {
        var day = scoringDay.Date;
        return db.Submissions.AnyAsync(s => s.UserId == userId && s.GameId == gameId && s.ScoringDay == day);
    }

    /// <summary>Submissions without a username are listed as <see cref="AnonymousUsername"/>.</summary>
    public Task<List<string>> GetUsernamesForDayAsync(int gameId, DateTime scoringDay) =>
        db.Submissions
            .ForGame(gameId)
            .OnScoringDay(scoringDay)
            .AsNoTracking()
            .Select(s => s.Username ?? AnonymousUsername)
            .ToListAsync();

    public async Task<Submission> CreateAsync(Submission submission)
    {
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();
        return submission;
    }

    public async Task<DateTime?> GetFirstScoringDayAsync(int gameId)
    {
        var first = await db.Submissions
            .ForGame(gameId)
            .OrderBy(s => s.ScoringDay)
            .Select(s => (DateTime?)s.ScoringDay)
            .FirstOrDefaultAsync();
        return first?.Date;
    }

    public Task<Submission?> GetByIdAsync(int id) =>
        db.Submissions.FirstOrDefaultAsync(s => s.Id == id);

    public Task<Submission?> GetByIdWithoutBlobAsync(int id) =>
        db.Submissions.Where(s => s.Id == id).SelectWithoutBlob().FirstOrDefaultAsync();

    public async Task<(byte[]? Data, string? ContentType)> GetBlobAsync(int id)
    {
        var row = await db.Submissions
            .Where(s => s.Id == id)
            .Select(s => new { s.ScreenshotData, s.ScreenshotContentType })
            .FirstOrDefaultAsync();
        return row == null ? (null, null) : (row.ScreenshotData, row.ScreenshotContentType);
    }

    /// <summary>Changes the score only; the screenshot is never replaced here.</summary>
    public async Task<Submission> UpdateAsync(Submission submission)
    {
        var existing = await db.Submissions.FirstOrDefaultAsync(s => s.Id == submission.Id)
            ?? throw new NotFoundException("Submission not found");

        existing.Score = submission.Score;
        existing.ScoreValue = submission.ScoreValue;
        await db.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        var existing = await db.Submissions.FindAsync(id);
        if (existing == null) return;

        db.Submissions.Remove(existing);
        await db.SaveChangesAsync();
    }

    public async Task<List<Submission>> GetByScoringDaysAsync(List<DateTime> scoringDays)
    {
        if (scoringDays == null || scoringDays.Count == 0) return [];

        var days = scoringDays.Select(d => d.Date).ToList();
        return await db.Submissions
            .Where(s => days.Contains(s.ScoringDay))
            .SelectWithoutBlob()
            .ToListAsync();
    }

    /// <summary>
    /// Counts per (game, day) in SQL for these games and days, then keeps the rows whose day is that game's
    /// own day (EF can't translate a Contains over tuples). At most games x distinct-days rows come back.
    /// </summary>
    public async Task<(int ActiveGameCount, int SubmissionCount)> CountOnGameDaysAsync(IReadOnlyDictionary<int, DateTime> dayByGame)
    {
        if (dayByGame == null || dayByGame.Count == 0) return (0, 0);

        var gameIds = dayByGame.Keys.ToList();
        var days = dayByGame.Values.Select(d => d.Date).Distinct().ToList();
        var counts = await db.Submissions
            .Where(s => gameIds.Contains(s.GameId) && days.Contains(s.ScoringDay))
            .GroupBy(s => new { s.GameId, s.ScoringDay })
            .Select(g => new { g.Key.GameId, g.Key.ScoringDay, Count = g.Count() })
            .ToListAsync();

        var onOwnDay = counts
            .Where(c => c.ScoringDay.Date == dayByGame[c.GameId].Date)
            .ToList();
        return (onOwnDay.Select(c => c.GameId).Distinct().Count(), onOwnDay.Sum(c => c.Count));
    }

    /// <summary>
    /// Two SQL queries: the games with the most recent activity of these users (grouped and limited), then
    /// the latest <see cref="RecentActivityQuery.PerGame"/> submissions of each of those games (a per-game window).
    /// </summary>
    public async Task<List<Submission>> GetRecentActivityByUsersAsync(RecentActivityQuery query)
    {
        var (userIds, since, maxGames, perGame) = query;
        var ids = userIds.ToList();
        if (ids.Count == 0 || maxGames < 1 || perGame < 1) return [];

        var gameIds = await GetMostRecentlyActiveGameIdsAsync(ids, since, maxGames);
        if (gameIds.Count == 0) return [];

        var rows = await db.Games
            .Where(g => gameIds.Contains(g.Id))
            .SelectMany(g => db.Submissions
                .Where(s => s.GameId == g.Id && s.UserId.HasValue && ids.Contains(s.UserId.Value) && s.CreatedAt >= since)
                .OrderByDescending(s => s.CreatedAt)
                .ThenByDescending(s => s.Id)
                .Take(perGame))
            .SelectWithoutBlob()
            .ToListAsync();

        var gameOrder = gameIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        return rows
            .OrderBy(s => gameOrder[s.GameId])
            .ThenByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .ToList();
    }

    private async Task<List<int>> GetMostRecentlyActiveGameIdsAsync(List<int> userIds, DateTime since, int maxGames)
    {
        var recentGames = await db.Submissions
            .Where(s => s.UserId.HasValue && userIds.Contains(s.UserId.Value) && s.CreatedAt >= since)
            .GroupBy(s => s.GameId)
            .Select(g => new { GameId = g.Key, LastAt = g.Max(s => s.CreatedAt) })
            .OrderByDescending(g => g.LastAt)
            .ThenBy(g => g.GameId)
            .Take(maxGames)
            .ToListAsync();
        return recentGames.Select(g => g.GameId).ToList();
    }

    public async Task<List<(int Id, string Score, double? ScoreValue)>> GetScoreRowsAsync()
    {
        var rows = await db.Submissions
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.Score, s.ScoreValue })
            .ToListAsync();
        return rows.Select(r => (r.Id, r.Score, r.ScoreValue)).ToList();
    }

    public Task<int> SetScoreValueAsync(int id, double? scoreValue) =>
        db.Submissions.Where(s => s.Id == id).ExecuteUpdateAsync(u => u.SetProperty(s => s.ScoreValue, scoreValue));

    public async Task<List<(int Id, int? UserId, DateTime CreatedAt, DateTime ScoringDay)>> GetScoringDayRowsAsync(int gameId, DateTime? afterScoringDay)
    {
        var query = db.Submissions.ForGame(gameId);
        if (afterScoringDay.HasValue)
        {
            var after = afterScoringDay.Value;
            query = query.Where(s => s.ScoringDay > after);
        }

        var rows = await query
            .Select(s => new { s.Id, s.UserId, s.CreatedAt, s.ScoringDay })
            .ToListAsync();
        return rows.Select(r => (r.Id, r.UserId, r.CreatedAt, r.ScoringDay)).ToList();
    }

    public Task SetScoringDayAsync(int id, DateTime scoringDay) =>
        db.Submissions.Where(s => s.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.ScoringDay, scoringDay));

    public Task<int> CountByUserAsync(int userId) =>
        db.Submissions.CountAsync(s => s.UserId == userId);
}
