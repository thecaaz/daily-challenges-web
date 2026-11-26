using DailyChallenges.Models;

namespace DailyChallenges.Repositories;

/// <summary>The submission filters the repository combines; each translates to SQL.</summary>
internal static class SubmissionQueryExtensions
{
    /// <summary>An integer search term also matches score values this close to it.</summary>
    private const double NumericSearchTolerance = 0.01;

    public static IQueryable<Submission> ForGame(this IQueryable<Submission> submissions, int gameId) =>
        submissions.Where(s => s.GameId == gameId);

    public static IQueryable<Submission> Scored(this IQueryable<Submission> submissions) =>
        submissions.Where(s => s.ScoreValue.HasValue);

    public static IQueryable<Submission> OnScoringDay(this IQueryable<Submission> submissions, DateTime scoringDay)
    {
        var day = scoringDay.Date;
        return submissions.Where(s => s.ScoringDay == day);
    }

    public static IQueryable<Submission> OnScoringDayIfGiven(this IQueryable<Submission> submissions, DateTime? scoringDay) =>
        scoringDay.HasValue ? submissions.OnScoringDay(scoringDay.Value) : submissions;

    public static IQueryable<Submission> ExcludingScoringDay(this IQueryable<Submission> submissions, DateTime? scoringDay)
    {
        if (!scoringDay.HasValue) return submissions;

        var day = scoringDay.Value.Date;
        return submissions.Where(s => s.ScoringDay != day);
    }

    public static IQueryable<Submission> NewestFirst(this IQueryable<Submission> submissions) =>
        submissions.OrderByDescending(s => s.CreatedAt);

    /// <summary>Case-insensitive match on username or score text; an integer term also matches nearby score values.</summary>
    public static IQueryable<Submission> MatchingSearch(this IQueryable<Submission> submissions, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return submissions;

        var term = search.ToLower();
        if (!int.TryParse(search, out var number))
            return submissions.Where(s =>
                (s.Username != null && s.Username.ToLower().Contains(term)) ||
                (s.Score != null && s.Score.ToLower().Contains(term)));

        return submissions.Where(s =>
            (s.Username != null && s.Username.ToLower().Contains(term)) ||
            (s.Score != null && s.Score.ToLower().Contains(term)) ||
            (s.ScoreValue.HasValue && s.ScoreValue >= number - NumericSearchTolerance && s.ScoreValue <= number + NumericSearchTolerance));
    }
}
